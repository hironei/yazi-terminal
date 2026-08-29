namespace YaziDesktopHost;

public static class YaziFileController
{
    public static async Task<bool> OpenAsync(
        string yaExecutable,
        string clientId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!await YaziCommandController.ExecuteAsync(
                yaExecutable,
                clientId,
                "reveal",
                [filePath],
                cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        return await YaziCommandController.ExecuteAsync(
                yaExecutable,
                clientId,
                "open",
                cancellationToken)
            .ConfigureAwait(false);
    }
}
