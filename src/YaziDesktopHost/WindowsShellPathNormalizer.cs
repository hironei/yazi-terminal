using System.IO;

namespace YaziDesktopHost;

public static class WindowsShellPathNormalizer
{
    public static bool TryNormalize(string value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value;
        if (candidate.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var fileUri)
                || !fileUri.IsFile)
            {
                return false;
            }

            candidate = fileUri.LocalPath;
        }
        else if (LooksLikeUri(candidate))
        {
            return false;
        }

        candidate = candidate.Replace('/', '\\');
        if (candidate.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            candidate = @"\" + candidate[7..];
        }
        else if (candidate.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            candidate = candidate[4..];
        }

        try
        {
            if (!Path.IsPathFullyQualified(candidate))
            {
                return false;
            }

            normalized = Path.GetFullPath(candidate);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool LooksLikeUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && !uri.IsFile
            && uri.Scheme.Length > 1;
    }
}
