using System.IO;

namespace YaziDesktopHost;

public sealed class YaziExecutableNotFoundException : Exception
{
    public YaziExecutableNotFoundException()
        : base("yazi.exe was not found.")
    {
    }
}

public static class YaziExecutableResolver
{
    public static string Resolve()
    {
        var explicitPath = Environment.GetEnvironmentVariable("YAZI_PATH");
        return Resolve(explicitPath, Environment.GetEnvironmentVariable("PATH"), File.Exists);
    }

    public static string ResolvePairedYa(string yaziPath)
    {
        return ResolvePairedYa(
            yaziPath,
            Environment.GetEnvironmentVariable("PATH"),
            File.Exists);
    }

    public static string ResolvePairedYa(
        string yaziPath,
        string? pathEnvironment,
        Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaziPath);
        ArgumentNullException.ThrowIfNull(fileExists);
        var fullYaziPath = Path.GetFullPath(yaziPath);
        var directory = Path.GetDirectoryName(fullYaziPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            var pairedPath = Path.Combine(directory, "ya.exe");
            if (fileExists(pairedPath))
            {
                return pairedPath;
            }
        }

        foreach (var pathDirectory in (pathEnvironment ?? string.Empty)
                     .Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(pathDirectory))
            {
                continue;
            }

            var candidate = Path.Combine(pathDirectory.Trim(), "ya.exe");
            if (fileExists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new YaziExecutableNotFoundException();
    }

    public static string Resolve(
        string? explicitPath,
        string? pathEnvironment,
        Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath.Trim());
            if (fileExists(fullPath))
            {
                return fullPath;
            }

            throw new YaziExecutableNotFoundException();
        }

        foreach (var directory in (pathEnvironment ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory.Trim(), "yazi.exe");
            if (fileExists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new YaziExecutableNotFoundException();
    }
}
