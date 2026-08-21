using System.IO;

namespace YaziDesktopHost;

public sealed class YaziBridgeSession : IAsyncDisposable
{
    private readonly Guid _instanceId;
    private readonly IYaziBridgeTransport _transport;
    private readonly YaziBridgeMessageParser _parser = new();
    private readonly YaziBridgeStateReducer _reducer;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    private Task? _runTask;
    private bool _disposed;

    public YaziBridgeSession(Guid instanceId, IYaziBridgeTransport transport)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("The bridge instance identifier must not be empty.", nameof(instanceId));
        }

        _instanceId = instanceId;
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _reducer = new YaziBridgeStateReducer(instanceId);
    }

    public event Action<YaziBridgeState?>? StateChanged;

    public event Action<string>? Disconnected;

    public YaziBridgeState? State => _reducer.State;

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_runTask is not null)
            {
                throw new InvalidOperationException("A bridge session can only be run once.");
            }

            _runTask = RunCoreAsync(cancellationToken);
            return _runTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? runTask;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            runTask = _runTask;
        }

        _shutdown.Cancel();
        _transport.Dispose();

        if (runTask is not null)
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown is an expected lifecycle outcome.
            }
        }

        _shutdown.Dispose();
    }

    private async Task RunCoreAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        IYaziBridgeConnection? connection = null;
        var reason = "disconnect";

        try
        {
            connection = await _transport.AcceptAsync(linkedCancellation.Token).ConfigureAwait(false);
            while (true)
            {
                var frame = await connection.ReadFrameAsync(linkedCancellation.Token).ConfigureAwait(false);
                if (frame is null)
                {
                    break;
                }

                YaziBridgeEnvelope message;
                try
                {
                    message = _parser.Parse(frame, _instanceId);
                    _reducer.Apply(message);
                }
                catch (YaziBridgeProtocolException)
                {
                    reason = "protocol-error";
                    break;
                }

                if (message.Kind is YaziBridgeMessageKind.Snapshot or YaziBridgeMessageKind.State)
                {
                    RaiseStateChanged(_reducer.State);
                }

                if (message.Kind is YaziBridgeMessageKind.Goodbye or YaziBridgeMessageKind.Error)
                {
                    reason = message.Kind == YaziBridgeMessageKind.Goodbye ? "goodbye" : "protocol-error";
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            reason = "cancelled";
        }
        catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
        {
            reason = "cancelled";
        }
        catch (IOException)
        {
            reason = "io-error";
        }
        finally
        {
            connection?.Dispose();
            _transport.Dispose();
            _reducer.MarkDisconnected();
            RaiseStateChanged(null);
            RaiseDisconnected(reason);
        }
    }

    private void RaiseStateChanged(YaziBridgeState? state)
    {
        try
        {
            StateChanged?.Invoke(state);
        }
        catch
        {
            // A feature subscriber must not terminate the bridge receive loop.
        }
    }

    private void RaiseDisconnected(string reason)
    {
        try
        {
            Disconnected?.Invoke(reason);
        }
        catch
        {
            // A feature subscriber must not change the session shutdown result.
        }
    }
}
