using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace YaziDesktopHost;

public sealed record LastInstanceEndpoint(string PipeName);

public sealed record LastInstanceControlRequest(string Path);

public static class LastInstanceControlProtocol
{
    public const string SupportedProtocol = "yazi-desktop-host/control/1";
    public const int MaxFrameBytes = 64 * 1024;

    public static string Serialize(LastInstanceControlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new ArgumentException("The directory path must not be empty.", nameof(request));
        }

        return JsonSerializer.Serialize(new
        {
            protocol = SupportedProtocol,
            command = "cd",
            path = request.Path,
        });
    }

    public static bool TryParse(string? frame, out LastInstanceControlRequest? request)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(frame)
            || Encoding.UTF8.GetByteCount(frame) > MaxFrameBytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(frame);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetString(root, "protocol", out var protocol)
                || !string.Equals(protocol, SupportedProtocol, StringComparison.Ordinal)
                || !TryGetString(root, "command", out var command)
                || !string.Equals(command, "cd", StringComparison.Ordinal)
                || !TryGetString(root, "path", out var path)
                || !Path.IsPathFullyQualified(path))
            {
                return false;
            }

            if (path.IndexOfAny(['\r', '\n']) >= 0)
            {
                return false;
            }

            request = new LastInstanceControlRequest(path);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string SerializeAcknowledgement(bool accepted)
    {
        return JsonSerializer.Serialize(new
        {
            protocol = SupportedProtocol,
            accepted,
        });
    }

    public static bool IsAcceptedAcknowledgement(string? frame)
    {
        if (string.IsNullOrWhiteSpace(frame))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(frame);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && TryGetString(root, "protocol", out var protocol)
                && string.Equals(protocol, SupportedProtocol, StringComparison.Ordinal)
                && root.TryGetProperty("accepted", out var accepted)
                && accepted.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement parent, string name, out string value)
    {
        if (parent.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString()))
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

internal static class LastInstanceFrame
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static async Task<string?> ReadAsync(
        Stream stream,
        int maxFrameBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameBytes);

        var bytes = new byte[maxFrameBytes];
        var singleByte = new byte[1];
        var count = 0;
        while (true)
        {
            var read = await stream.ReadAsync(singleByte.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (count == 0)
                {
                    return null;
                }

                throw new InvalidDataException("The control frame ended before its newline delimiter.");
            }

            if (singleByte[0] == (byte)'\n')
            {
                if (count > 0 && bytes[count - 1] == (byte)'\r')
                {
                    count--;
                }

                try
                {
                    return StrictUtf8.GetString(bytes, 0, count);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException("The control frame is not valid UTF-8.", exception);
                }
            }

            if (count == maxFrameBytes)
            {
                throw new InvalidDataException("The control frame exceeds the maximum size.");
            }

            bytes[count++] = singleByte[0];
        }
    }

    public static async Task WriteAsync(
        Stream stream,
        string frame,
        int maxFrameBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFrameBytes);

        var payload = StrictUtf8.GetBytes(frame);
        if (payload.Length > maxFrameBytes)
        {
            throw new InvalidDataException("The control frame exceeds the maximum size.");
        }

        var framed = new byte[payload.Length + 1];
        payload.CopyTo(framed, 0);
        framed[^1] = (byte)'\n';
        await stream.WriteAsync(framed.AsMemory(), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class LastInstanceRegistry
{
    private const string MetadataProtocol = "yazi-desktop-host/last-instance/1";
    private const int MaxMetadataBytes = 4096;
    private readonly string _metadataPath;
    private readonly string _mutexName;

    public LastInstanceRegistry(string? metadataPath = null, string? mutexName = null)
    {
        _metadataPath = metadataPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YaziTerminal",
            "last-instance.json");
        _mutexName = mutexName ?? @"Local\YaziTerminal-last-instance";
    }

    public bool TryRead(out LastInstanceEndpoint? endpoint)
    {
        LastInstanceEndpoint? found = null;
        var success = WithLock(() => TryReadWithoutLock(out found));
        endpoint = found;
        return success;
    }

    public bool Publish(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        try
        {
            return WithLock(() =>
            {
                var directory = Path.GetDirectoryName(_metadataPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temporaryPath = $"{_metadataPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    var metadata = new LastInstanceMetadata(MetadataProtocol, pipeName);
                    File.WriteAllText(temporaryPath, JsonSerializer.Serialize(metadata), new UTF8Encoding(false));
                    File.Move(temporaryPath, _metadataPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }

                return true;
            });
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool RemoveIfCurrent(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        try
        {
            return WithLock(() =>
            {
                if (!TryReadWithoutLock(out var endpoint)
                    || !string.Equals(endpoint!.PipeName, pipeName, StringComparison.Ordinal))
                {
                    return false;
                }

                try
                {
                    File.Delete(_metadataPath);
                }
                catch (FileNotFoundException)
                {
                    // Another cleanup already removed the record.
                }
                catch (DirectoryNotFoundException)
                {
                    // The application-data directory was removed externally.
                }
                catch (IOException exception)
                {
                    AppLogger.Log("last_instance_registry_cleanup_failed", exception);
                    return false;
                }
                catch (UnauthorizedAccessException exception)
                {
                    AppLogger.Log("last_instance_registry_cleanup_failed", exception);
                    return false;
                }

                return true;
            });
        }
        catch (IOException exception)
        {
            AppLogger.Log("last_instance_registry_cleanup_failed", exception);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLogger.Log("last_instance_registry_cleanup_failed", exception);
            return false;
        }
    }

    private bool TryReadWithoutLock(out LastInstanceEndpoint? endpoint)
    {
        endpoint = null;
        try
        {
            if (!File.Exists(_metadataPath)
                || new FileInfo(_metadataPath).Length > MaxMetadataBytes)
            {
                return false;
            }

            var metadata = JsonSerializer.Deserialize<LastInstanceMetadata>(File.ReadAllText(_metadataPath));
            if (metadata is null
                || !string.Equals(metadata.Protocol, MetadataProtocol, StringComparison.Ordinal)
                || !IsControlPipeName(metadata.PipeName))
            {
                return false;
            }

            endpoint = new LastInstanceEndpoint(metadata.PipeName);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool WithLock(Func<bool> action)
    {
        using var mutex = new Mutex(false, _mutexName);
        var lockAcquired = false;
        try
        {
            lockAcquired = mutex.WaitOne(TimeSpan.FromSeconds(1));
        }
        catch (AbandonedMutexException)
        {
            // Ownership was acquired from a terminated process.
            lockAcquired = true;
        }

        if (!lockAcquired)
        {
            return false;
        }

        try
        {
            return action();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    internal static bool IsControlPipeName(string? pipeName)
    {
        const string prefix = "yazi-terminal-control-";
        const int guidHexLength = 32;
        if (pipeName is null
            || pipeName.Length != prefix.Length + guidHexLength
            || !pipeName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = prefix.Length; index < pipeName.Length; index++)
        {
            var character = pipeName[index];
            if (!((character is >= '0' and <= '9')
                || (character is >= 'a' and <= 'f')
                || (character is >= 'A' and <= 'F')))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record LastInstanceMetadata(string Protocol, string PipeName);
}

public static class LastInstanceClient
{
    public static bool TrySend(
        LastInstanceEndpoint endpoint,
        string directory,
        TimeSpan timeout)
    {
        return TrySendAsync(endpoint, directory, timeout).GetAwaiter().GetResult();
    }

    public static async Task<bool> TrySendAsync(
        LastInstanceEndpoint endpoint,
        string directory,
        TimeSpan timeout)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            if (!LastInstanceRegistry.IsControlPipeName(endpoint.PipeName)
                || timeout <= TimeSpan.Zero)
            {
                return false;
            }

            using var timeoutCancellation = new CancellationTokenSource(timeout);
            using var client = new NamedPipeClientStream(
                ".",
                endpoint.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(timeoutCancellation.Token).ConfigureAwait(false);

            var request = LastInstanceControlProtocol.Serialize(new LastInstanceControlRequest(directory));
            await LastInstanceFrame.WriteAsync(
                client,
                request,
                LastInstanceControlProtocol.MaxFrameBytes,
                timeoutCancellation.Token).ConfigureAwait(false);
            var acknowledgement = await LastInstanceFrame.ReadAsync(
                client,
                LastInstanceControlProtocol.MaxFrameBytes,
                timeoutCancellation.Token).ConfigureAwait(false);
            return LastInstanceControlProtocol.IsAcceptedAcknowledgement(acknowledgement);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class LastInstanceControlServer : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
    private readonly LastInstanceRegistry _registry;
    private readonly CancellationTokenSource _shutdown = new();
    private NamedPipeServerStream? _server;
    private Task? _runTask;
    private bool _disposed;

    public LastInstanceControlServer(LastInstanceRegistry? registry = null)
    {
        _registry = registry ?? new LastInstanceRegistry();
        PipeName = $"yazi-terminal-control-{Guid.NewGuid():N}";
    }

    public string PipeName { get; }

    public event Func<string, CancellationToken, Task<bool>>? DirectoryRequested;

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_runTask is not null)
        {
            throw new InvalidOperationException("The last-instance control server can only be started once.");
        }

        NamedPipeServerStream? server = null;
        try
        {
            server = CreateServer(PipeName);
            _server = server;
            if (!_registry.Publish(PipeName))
            {
                AppLogger.Log("last_instance_control_publish_failed");
                DisableAfterStartFailure(server);
                return false;
            }

            _runTask = RunAsync(server);
            return true;
        }
        catch (Exception exception)
        {
            AppLogger.Log("last_instance_control_start_failed", exception);
            DisableAfterStartFailure(server);
            return false;
        }
    }

    private void DisableAfterStartFailure(NamedPipeServerStream? server)
    {
        try
        {
            server?.Dispose();
        }
        catch (Exception exception)
        {
            AppLogger.Log("last_instance_control_server_cleanup_failed", exception);
        }

        _server = null;
        try
        {
            _registry.RemoveIfCurrent(PipeName);
        }
        catch (Exception exception)
        {
            AppLogger.Log("last_instance_control_registry_cleanup_failed", exception);
        }

        _shutdown.Cancel();
        _shutdown.Dispose();
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _server?.Dispose();
        _server = null;
        _registry.RemoveIfCurrent(PipeName);
        _shutdown.Dispose();
    }

    private async Task RunAsync(NamedPipeServerStream server)
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    await server.WaitForConnectionAsync(_shutdown.Token).ConfigureAwait(false);
                    await HandleConnectionAsync(server, _shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException)
                {
                    // A failed client connection does not terminate the host.
                }
                finally
                {
                    server.Dispose();
                }

                if (_shutdown.IsCancellationRequested)
                {
                    break;
                }

                server = CreateServer(PipeName);
                _server = server;
            }
        }
        catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task HandleConnectionAsync(
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(RequestTimeout);

        string? frame;
        try
        {
            frame = await LastInstanceFrame.ReadAsync(
                server,
                LastInstanceControlProtocol.MaxFrameBytes,
                requestTimeout.Token).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            await TryWriteAcknowledgementAsync(server, false, requestTimeout.Token).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!LastInstanceControlProtocol.TryParse(frame, out var request))
        {
            await TryWriteAcknowledgementAsync(server, false, requestTimeout.Token).ConfigureAwait(false);
            return;
        }

        var accepted = false;
        try
        {
            var handler = DirectoryRequested;
            if (handler is not null)
            {
                accepted = await handler(request!.Path, requestTimeout.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            accepted = false;
        }

        await TryWriteAcknowledgementAsync(server, accepted, requestTimeout.Token).ConfigureAwait(false);
    }

    private static async Task TryWriteAcknowledgementAsync(
        Stream server,
        bool accepted,
        CancellationToken cancellationToken)
    {
        try
        {
            await LastInstanceFrame.WriteAsync(
                server,
                LastInstanceControlProtocol.SerializeAcknowledgement(accepted),
                LastInstanceControlProtocol.MaxFrameBytes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The client may have timed out and disconnected before the ACK.
        }
        catch (OperationCanceledException)
        {
            // The request or server was cancelled.
        }
    }

    private static NamedPipeServerStream CreateServer(string pipeName)
    {
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }
}
