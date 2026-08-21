using System.Text;
using System.Text.Json;

namespace YaziDesktopHost;

public enum YaziBridgeMessageKind
{
    Hello,
    Snapshot,
    State,
    Goodbye,
    Error,
}

public enum YaziBridgePathKind
{
    Filesystem,
    Url,
}

public enum YaziBridgeAvailability
{
    Unavailable,
    Available,
}

public sealed record YaziBridgePath(YaziBridgePathKind Kind, string Value);

public sealed record YaziBridgeEnvelope(
    string Protocol,
    Guid InstanceId,
    ulong Sequence,
    YaziBridgeMessageKind Kind,
    JsonElement Payload);

public sealed record YaziBridgeState(
    Guid InstanceId,
    ulong Sequence,
    int Tab,
    YaziBridgePath Cwd,
    YaziBridgePath? Hovered,
    IReadOnlyList<YaziBridgePath> Selected,
    YaziBridgeAvailability Availability);

public sealed class YaziBridgeProtocolException : Exception
{
    public YaziBridgeProtocolException(string message)
        : base(message)
    {
    }
}

public sealed class YaziBridgeMessageParser
{
    public const string SupportedProtocol = "yazi-desktop-host/1";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public YaziBridgeEnvelope Parse(ReadOnlySpan<byte> frame, Guid expectedInstanceId)
    {
        if (frame.Length == 0 || frame.Length > 64 * 1024)
        {
            throw new YaziBridgeProtocolException("Bridge frame size is invalid.");
        }

        string json;
        try
        {
            json = StrictUtf8.GetString(frame);
        }
        catch (DecoderFallbackException exception)
        {
            throw new YaziBridgeProtocolException($"Bridge frame is not valid UTF-8: {exception.Message}");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            RequireObject(root, "envelope");

            var protocol = RequiredString(root, "protocol");
            if (!string.Equals(protocol, SupportedProtocol, StringComparison.Ordinal))
            {
                throw new YaziBridgeProtocolException("Bridge protocol version is not supported.");
            }

            if (!Guid.TryParse(RequiredString(root, "instanceId"), out var instanceId))
            {
                throw new YaziBridgeProtocolException("Bridge instanceId is invalid.");
            }

            if (instanceId != expectedInstanceId)
            {
                throw new YaziBridgeProtocolException("Bridge instanceId does not match the pending session.");
            }

            var sequenceElement = RequiredProperty(root, "sequence");
            if (!sequenceElement.TryGetUInt64(out var sequence))
            {
                throw new YaziBridgeProtocolException("Bridge sequence is invalid.");
            }

            var kind = ParseKind(RequiredString(root, "kind"));
            var payload = RequiredProperty(root, "payload");
            RequireObject(payload, "payload");

            return new YaziBridgeEnvelope(protocol, instanceId, sequence, kind, payload.Clone());
        }
        catch (JsonException exception)
        {
            throw new YaziBridgeProtocolException($"Bridge frame JSON is invalid: {exception.Message}");
        }
    }

    private static YaziBridgeMessageKind ParseKind(string value) => value switch
    {
        "hello" => YaziBridgeMessageKind.Hello,
        "snapshot" => YaziBridgeMessageKind.Snapshot,
        "state" => YaziBridgeMessageKind.State,
        "goodbye" => YaziBridgeMessageKind.Goodbye,
        "error" => YaziBridgeMessageKind.Error,
        _ => throw new YaziBridgeProtocolException("Bridge message kind is unknown."),
    };

    private static string RequiredString(JsonElement parent, string propertyName)
    {
        var property = RequiredProperty(parent, propertyName);
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new YaziBridgeProtocolException($"Bridge property '{propertyName}' must be a non-empty string.");
        }

        return property.GetString()!;
    }

    private static JsonElement RequiredProperty(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new YaziBridgeProtocolException($"Bridge property '{propertyName}' is required.");
        }

        return property;
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new YaziBridgeProtocolException($"Bridge {name} must be a JSON object.");
        }
    }
}

public sealed class YaziBridgeStateReducer
{
    private readonly Guid _instanceId;
    private YaziBridgeState? _state;
    private bool _handshakeCompleted;

    public YaziBridgeStateReducer(Guid instanceId)
    {
        _instanceId = instanceId;
    }

    public YaziBridgeState? State => _state;

    public YaziBridgeAvailability Availability => _state?.Availability ?? YaziBridgeAvailability.Unavailable;

    public string? UnavailableReason { get; private set; } = "handshake-required";

    public void Apply(YaziBridgeEnvelope message)
    {
        if (message.InstanceId != _instanceId)
        {
            throw new YaziBridgeProtocolException("Bridge instanceId does not match the reducer.");
        }

        switch (message.Kind)
        {
            case YaziBridgeMessageKind.Hello:
                if (_handshakeCompleted)
                {
                    MarkUnavailable("duplicate-hello");
                    return;
                }

                _handshakeCompleted = true;
                return;
            case YaziBridgeMessageKind.Snapshot:
                if (!_handshakeCompleted)
                {
                    MarkUnavailable("handshake-required");
                    return;
                }

                try
                {
                    _state = ParseSnapshot(message);
                    UnavailableReason = null;
                }
                catch (YaziBridgeProtocolException)
                {
                    MarkUnavailable("invalid-snapshot");
                    throw;
                }

                return;
            case YaziBridgeMessageKind.State:
                ApplyStateUpdate(message);
                return;
            case YaziBridgeMessageKind.Goodbye:
                MarkUnavailable("goodbye");
                return;
            case YaziBridgeMessageKind.Error:
                MarkUnavailable("protocol-error");
                return;
            default:
                throw new YaziBridgeProtocolException("Bridge message kind is not handled.");
        }
    }

    public void MarkDisconnected()
    {
        MarkUnavailable("disconnect");
    }

    private void ApplyStateUpdate(YaziBridgeEnvelope message)
    {
        if (_state is null || _state.Availability != YaziBridgeAvailability.Available)
        {
            MarkUnavailable("snapshot-required");
            return;
        }

        if (message.Sequence != _state.Sequence + 1)
        {
            MarkUnavailable("sequence-gap");
            return;
        }

        var payload = message.Payload;
        var present = RequiredStringArray(payload, "present");
        if (present.Count == 0)
        {
            MarkUnavailable("empty-state-update");
            return;
        }

        var tab = _state.Tab;
        var cwd = _state.Cwd;
        var hovered = _state.Hovered;
        var selected = _state.Selected;

        foreach (var field in present)
        {
            switch (field)
            {
                case "tab":
                    tab = RequiredNonNegativeInt(payload, "tab");
                    break;
                case "cwd":
                    cwd = ParsePath(RequiredProperty(payload, "cwd"), "cwd");
                    break;
                case "hovered":
                    hovered = ParseNullablePath(RequiredProperty(payload, "hovered"), "hovered");
                    break;
                case "selected":
                    selected = ParsePathArray(RequiredProperty(payload, "selected"), "selected");
                    break;
                default:
                    MarkUnavailable("unknown-state-field");
                    return;
            }
        }

        _state = new YaziBridgeState(
            _instanceId,
            message.Sequence,
            tab,
            cwd,
            hovered,
            selected,
            YaziBridgeAvailability.Available);
        UnavailableReason = null;
    }

    private YaziBridgeState ParseSnapshot(YaziBridgeEnvelope message)
    {
        var payload = message.Payload;
        return new YaziBridgeState(
            _instanceId,
            message.Sequence,
            RequiredNonNegativeInt(payload, "tab"),
            ParsePath(RequiredProperty(payload, "cwd"), "cwd"),
            ParseNullablePath(RequiredProperty(payload, "hovered"), "hovered"),
            ParsePathArray(RequiredProperty(payload, "selected"), "selected"),
            YaziBridgeAvailability.Available);
    }

    private void MarkUnavailable(string reason)
    {
        _state = null;
        _handshakeCompleted = false;
        UnavailableReason = reason;
    }

    private static YaziBridgePath ParsePath(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new YaziBridgeProtocolException($"Bridge path '{name}' must be an object.");
        }

        var kind = RequiredString(value, "kind") switch
        {
            "filesystem" => YaziBridgePathKind.Filesystem,
            "url" => YaziBridgePathKind.Url,
            _ => throw new YaziBridgeProtocolException($"Bridge path '{name}' has an unknown kind."),
        };
        var path = RequiredString(value, "value");
        return new YaziBridgePath(kind, path);
    }

    private static YaziBridgePath? ParseNullablePath(JsonElement value, string name)
    {
        return value.ValueKind == JsonValueKind.Null ? null : ParsePath(value, name);
    }

    private static IReadOnlyList<YaziBridgePath> ParsePathArray(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new YaziBridgeProtocolException($"Bridge path list '{name}' must be an array.");
        }

        return value.EnumerateArray().Select(item => ParsePath(item, name)).ToArray();
    }

    private static IReadOnlyList<string> RequiredStringArray(JsonElement parent, string name)
    {
        var value = RequiredProperty(parent, name);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new YaziBridgeProtocolException($"Bridge field '{name}' must be an array.");
        }

        return value.EnumerateArray().Select(item =>
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new YaziBridgeProtocolException($"Bridge field '{name}' contains an invalid value.");
            }

            return item.GetString()!;
        }).ToArray();
    }

    private static int RequiredNonNegativeInt(JsonElement parent, string name)
    {
        var value = RequiredProperty(parent, name);
        if (!value.TryGetInt32(out var number) || number < 0)
        {
            throw new YaziBridgeProtocolException($"Bridge field '{name}' must be a non-negative integer.");
        }

        return number;
    }

    private static string RequiredString(JsonElement parent, string name)
    {
        var value = RequiredProperty(parent, name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new YaziBridgeProtocolException($"Bridge property '{name}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static JsonElement RequiredProperty(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw new YaziBridgeProtocolException($"Bridge property '{name}' is required.");
        }

        return value;
    }
}
