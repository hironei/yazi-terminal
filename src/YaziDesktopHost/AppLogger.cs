using System.IO;

namespace YaziDesktopHost;

internal static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static void Log(string eventName, Exception? exception = null)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YaziDesktopHost");
            Directory.CreateDirectory(logDirectory);

            var line = $"{DateTimeOffset.Now:O} {eventName}";
            if (exception is not null)
            {
                line += $" {exception.GetType().Name}";
            }

            lock (SyncRoot)
            {
                File.AppendAllText(Path.Combine(logDirectory, "app.log"), line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never prevent the GUI from reporting the original error.
        }
    }
}
