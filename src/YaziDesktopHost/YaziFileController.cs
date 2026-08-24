namespace YaziDesktopHost;

public static class YaziFileController
{
    public static string CreateRevealCommand(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return $"reveal \"{filePath}\"";
    }

    public static async Task<bool> OpenAsync(
        string yaExecutable,
        string clientId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var revealCommand = CreateRevealCommand(filePath);
        if (!await YaziCommandController.ExecuteAsync(
                yaExecutable,
                clientId,
                revealCommand,
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
