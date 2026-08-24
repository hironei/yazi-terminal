namespace YaziDesktopHost;

internal enum YaziPathRequestKind
{
    ChangeDirectory,
    OpenFile,
}

internal sealed record YaziPathRequest(YaziPathRequestKind Kind, string Path);

internal interface IYaziPathTransactionController
{
    Task<bool> ChangeDirectoryAsync(string directory, CancellationToken cancellationToken);

    Task<bool> OpenFileAsync(string filePath, CancellationToken cancellationToken);
}

internal sealed class YaziPathRequestSequencer
{
    private readonly IYaziPathTransactionController _controller;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public YaziPathRequestSequencer(IYaziPathTransactionController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public async Task<bool> ExecuteAsync(
        YaziPathRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Path);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return request.Kind switch
            {
                YaziPathRequestKind.ChangeDirectory => await _controller
                    .ChangeDirectoryAsync(request.Path, cancellationToken)
                    .ConfigureAwait(false),
                YaziPathRequestKind.OpenFile => await _controller
                    .OpenFileAsync(request.Path, cancellationToken)
                    .ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, null),
            };
        }
        finally
        {
            _gate.Release();
        }
    }
}

internal sealed class YaziProcessPathTransactionController : IYaziPathTransactionController
{
    private readonly string _yaExecutable;
    private readonly string _clientId;

    public YaziProcessPathTransactionController(string yaExecutable, string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        _yaExecutable = yaExecutable;
        _clientId = clientId;
    }

    public Task<bool> ChangeDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        return YaziDirectoryController.ChangeDirectoryAsync(
            _yaExecutable,
            _clientId,
            directory,
            cancellationToken);
    }

    public Task<bool> OpenFileAsync(string filePath, CancellationToken cancellationToken)
    {
        return YaziFileController.OpenAsync(
            _yaExecutable,
            _clientId,
            filePath,
            cancellationToken);
    }
}
