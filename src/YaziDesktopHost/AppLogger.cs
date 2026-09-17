using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace YaziDesktopHost;

internal static class AppLogger
{
    internal const long MaxLogBytes = 1 * 1024 * 1024;

    private static readonly object SyncRoot = new();

    public static void Log(string eventName, Exception? exception = null)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YaziTerminal");
            Directory.CreateDirectory(logDirectory);

            var line = $"{DateTimeOffset.Now:O} {eventName}";
            if (exception is not null)
            {
                line += $" {exception.GetType().Name}";
                if (exception is ArgumentException argument
                    && !string.IsNullOrWhiteSpace(argument.ParamName))
                {
                    line += $" param={argument.ParamName}";
                }

                if (exception.HResult != 0)
                {
                    line += $" hresult=0x{exception.HResult:X8}";
                }
            }

            AppendLine(Path.Combine(logDirectory, "app.log"), line + Environment.NewLine, MaxLogBytes);
        }
        catch
        {
            // Logging must never prevent the GUI from reporting the original error.
        }
    }

    internal static void AppendLine(string path, string line, long maxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        lock (SyncRoot)
        {
            using var mutex = new Mutex(false, CreateMutexName(path));
            var acquired = false;
            try
            {
                try
                {
                    acquired = mutex.WaitOne(TimeSpan.FromSeconds(2));
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired)
                {
                    throw new IOException("Could not acquire the application log mutex.");
                }

                if (File.Exists(path) && new FileInfo(path).Length >= maxBytes)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }

                File.AppendAllText(path, line);
            }
            finally
            {
                if (acquired)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }

    private static string CreateMutexName(string path)
    {
        var normalizedPath = Path.GetFullPath(path).ToUpperInvariant();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
        return $@"Local\YaziTerminal-log-{digest[..32]}";
    }
}
