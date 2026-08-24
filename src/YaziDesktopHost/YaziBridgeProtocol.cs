using System.Buffers;
using System.IO;
using System.IO.Pipes;
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

public sealed record YaziBridgeCommand(string Key, string Run, string Description);

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
    public const int MaxFrameBytes = 64 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public YaziBridgeEnvelope Parse(ReadOnlySpan<byte> frame, Guid expectedInstanceId)
    {
        if (frame.Length == 0 || frame.Length > MaxFrameBytes)
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

public sealed class YaziBridgeCommandCatalogParser
{
    private const int MaxCommands = 512;
    private const int MaxCommandTextLength = 4096;

    public IReadOnlyList<YaziBridgeCommand> Parse(JsonElement helloPayload)
    {
        if (helloPayload.ValueKind != JsonValueKind.Object)
        {
            throw new YaziBridgeProtocolException("Bridge hello payload must be an object.");
        }

        if (!helloPayload.TryGetProperty("commands", out var commands))
        {
            return Array.Empty<YaziBridgeCommand>();
        }

        if (commands.ValueKind != JsonValueKind.Array || commands.GetArrayLength() > MaxCommands)
        {
            throw new YaziBridgeProtocolException("Bridge command catalog is invalid.");
        }

        var result = new List<YaziBridgeCommand>(commands.GetArrayLength());
        foreach (var command in commands.EnumerateArray())
        {
            if (command.ValueKind != JsonValueKind.Object)
            {
                throw new YaziBridgeProtocolException("Bridge command entry must be an object.");
            }

            var key = OptionalString(command, "key");
            var run = RequiredBoundedString(command, "run");
            var description = OptionalString(command, "description") ?? string.Empty;
            if (description.Length > MaxCommandTextLength)
            {
                throw new YaziBridgeProtocolException("Bridge command description is too long.");
            }

            result.Add(new YaziBridgeCommand(key ?? string.Empty, run, description));
        }

        return result;
    }

    private static string? OptionalString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new YaziBridgeProtocolException($"Bridge command property '{name}' must be a string.");
        }

        return value.GetString();
    }

    private static string RequiredBoundedString(JsonElement parent, string name)
    {
        var value = OptionalString(parent, name);
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxCommandTextLength)
        {
            throw new YaziBridgeProtocolException($"Bridge command property '{name}' is invalid.");
        }

        return value;
    }
}

public interface IYaziBridgeTransport : IDisposable
{
    string PipeName { get; }

    Task<IYaziBridgeConnection> AcceptAsync(CancellationToken cancellationToken = default);
}

public interface IYaziBridgeConnection : IAsyncDisposable, IDisposable
{
    Task<byte[]?> ReadFrameAsync(CancellationToken cancellationToken = default);

    Task WriteFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);
}

public sealed class YaziBridgePipeServer : IYaziBridgeTransport
{
    private readonly Guid _instanceId;
    private NamedPipeServerStream? _server;
    private bool _disposed;

    public YaziBridgePipeServer(Guid instanceId)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("The bridge instance identifier must not be empty.", nameof(instanceId));
        }

        _instanceId = instanceId;
        PipeName = $"yazi-desktop-host-{instanceId:N}";
    }

    public string PipeName { get; }

    public string PipePath => $@"\\.\pipe\{PipeName}";

    public async Task<IYaziBridgeConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var server = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _server = server;

        try
        {
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            _server = null;
            return new YaziBridgePipeConnection(server);
        }
        catch
        {
            server.Dispose();
            _server = null;
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _server?.Dispose();
        _server = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed class YaziBridgePipeConnection : IYaziBridgeConnection
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly byte[] _readBuffer = new byte[4096];
    private int _readOffset;
    private int _readCount;
    private bool _disposed;

    internal YaziBridgePipeConnection(Stream stream)
    {
        _stream = stream;
    }

    public async Task<byte[]?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var frame = new ArrayBufferWriter<byte>();

        while (true)
        {
            if (_readOffset == _readCount)
            {
                _readOffset = 0;
                _readCount = await _stream.ReadAsync(_readBuffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (_readCount == 0)
                {
                    if (frame.WrittenCount == 0)
                    {
                        return null;
                    }

                    throw new YaziBridgeProtocolException("Bridge connection ended in the middle of a frame.");
                }
            }

            var value = _readBuffer[_readOffset++];
            if (value == (byte)'\n')
            {
                var result = frame.WrittenSpan;
                if (result.Length > 0 && result[^1] == (byte)'\r')
                {
                    return result[..^1].ToArray();
                }

                return result.ToArray();
            }

            if (frame.WrittenCount >= YaziBridgeMessageParser.MaxFrameBytes)
            {
                throw new YaziBridgeProtocolException("Bridge frame exceeds the maximum size.");
            }

            frame.GetSpan(1)[0] = value;
            frame.Advance(1);
        }
    }

    public async Task WriteFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (frame.Length == 0 || frame.Length > YaziBridgeMessageParser.MaxFrameBytes)
        {
            throw new YaziBridgeProtocolException("Bridge frame size is invalid.");
        }

        if (frame.Span.IndexOfAny((byte)'\r', (byte)'\n') >= 0)
        {
            throw new YaziBridgeProtocolException("Bridge frame contains a line break.");
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
        _writeGate.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed class YaziBridgeStateReducer
{
    private readonly Guid _instanceId;
    private YaziBridgeState? _state;
    private bool _handshakeCompleted;
    private bool _snapshotAccepted;
    private bool _connectionRejected;
    private ulong? _lastSequence;

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

        if (_connectionRejected || !AcceptSequence(message.Sequence))
        {
            return;
        }

        switch (message.Kind)
        {
            case YaziBridgeMessageKind.Hello:
                if (_handshakeCompleted)
                {
                    RejectConnection("duplicate-hello");
                    return;
                }

                _handshakeCompleted = true;
                return;
            case YaziBridgeMessageKind.Snapshot:
                if (!_handshakeCompleted || _snapshotAccepted)
                {
                    RejectConnection(_handshakeCompleted ? "duplicate-snapshot" : "handshake-required");
                    return;
                }

                try
                {
                    _state = ParseSnapshot(message);
                    _snapshotAccepted = true;
                    UnavailableReason = null;
                }
                catch (YaziBridgeProtocolException)
                {
                    RejectConnection("invalid-snapshot");
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
        _state = null;
        _handshakeCompleted = false;
        _snapshotAccepted = false;
        _connectionRejected = false;
        _lastSequence = null;
        UnavailableReason = "disconnect";
    }

    private void ApplyStateUpdate(YaziBridgeEnvelope message)
    {
        if (_state is null || _state.Availability != YaziBridgeAvailability.Available)
        {
            RejectConnection("snapshot-required");
            return;
        }

        var payload = message.Payload;
        var present = RequiredStringArray(payload, "present");
        if (present.Count == 0)
        {
            RejectConnection("empty-state-update");
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
                    RejectConnection("unknown-state-field");
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
        UnavailableReason = reason;
    }

    private bool AcceptSequence(ulong sequence)
    {
        if (_lastSequence is ulong previous
            && (previous == ulong.MaxValue || sequence != previous + 1))
        {
            RejectConnection("sequence-gap");
            return false;
        }

        _lastSequence = sequence;
        return true;
    }

    private void RejectConnection(string reason)
    {
        MarkUnavailable(reason);
        _connectionRejected = true;
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
