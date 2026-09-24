using System.IO;

namespace YaziDesktopHost;

public sealed class WindowsShellFinalPathResolver
{
    internal const int MaxLinkResolutions = 16;

    public static bool TryResolve(
        YaziShellTarget target,
        out YaziShellTarget resolvedTarget,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(target);

        var resolvedPaths = new List<string>(target.Paths.Count);
        foreach (var path in target.Paths)
        {
            if (!TryResolvePath(
                    path,
                    ProbeLinkTarget,
                    MaxLinkResolutions,
                    out var resolvedPath,
                    out reason))
            {
                resolvedTarget = null!;
                return false;
            }

            resolvedPaths.Add(resolvedPath);
        }

        resolvedTarget = target with { Paths = resolvedPaths };
        reason = string.Empty;
        return true;
    }

    internal static bool TryResolvePath(
        string path,
        Func<string, LinkTargetProbe> probe,
        int maxLinkResolutions,
        out string resolvedPath,
        out string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(probe);
        if (maxLinkResolutions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLinkResolutions));
        }

        try
        {
            var current = Path.GetFullPath(path);
            for (var count = 0; count < maxLinkResolutions; count++)
            {
                if (!TryFindNearestLink(current, probe, out var linkPath, out var target, out reason))
                {
                    resolvedPath = string.Empty;
                    return false;
                }

                if (linkPath is null || target is null)
                {
                    resolvedPath = current;
                    reason = string.Empty;
                    return true;
                }

                var relativePath = Path.GetRelativePath(linkPath, current);
                current = string.Equals(relativePath, ".", StringComparison.Ordinal)
                    ? target
                    : Path.GetFullPath(Path.Combine(target, relativePath));
            }

            resolvedPath = string.Empty;
            reason = "resolution-limit";
            return false;
        }
        catch (ArgumentException)
        {
            resolvedPath = string.Empty;
            reason = "invalid-filesystem-path";
            return false;
        }
        catch (IOException)
        {
            resolvedPath = string.Empty;
            reason = "link-resolution-failed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            resolvedPath = string.Empty;
            reason = "link-resolution-denied";
            return false;
        }
    }

    private static bool TryFindNearestLink(
        string path,
        Func<string, LinkTargetProbe> probe,
        out string? linkPath,
        out string? target,
        out string reason)
    {
        var candidate = path;
        while (!string.IsNullOrEmpty(candidate))
        {
            var result = probe(candidate);
            if (!result.Succeeded)
            {
                linkPath = null;
                target = null;
                reason = "link-resolution-failed";
                return false;
            }

            if (result.IsLink)
            {
                if (string.IsNullOrWhiteSpace(result.Target))
                {
                    linkPath = null;
                    target = null;
                    reason = "link-target-missing";
                    return false;
                }

                linkPath = candidate;
                target = Path.IsPathFullyQualified(result.Target)
                    ? Path.GetFullPath(result.Target)
                    : Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(candidate) ?? string.Empty,
                        result.Target));
                reason = string.Empty;
                return true;
            }

            var parent = Path.GetDirectoryName(candidate);
            if (string.IsNullOrEmpty(parent)
                || string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            candidate = parent;
        }

        linkPath = null;
        target = null;
        reason = string.Empty;
        return true;
    }

    private static LinkTargetProbe ProbeLinkTarget(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return LinkTargetProbe.NotLink;
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException)
        {
            return LinkTargetProbe.Failed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return LinkTargetProbe.Failed;
        }

        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            return LinkTargetProbe.NotLink;
        }

        try
        {
            FileSystemInfo info = (attributes & FileAttributes.Directory) != 0
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            var target = info.ResolveLinkTarget(returnFinalTarget: false);
            return target is null
                ? LinkTargetProbe.NotLink
                : new LinkTargetProbe(true, true, target.FullName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return LinkTargetProbe.Failed;
        }
    }

    internal readonly record struct LinkTargetProbe(
        bool Succeeded,
        bool IsLink,
        string? Target)
    {
        public static LinkTargetProbe NotLink => new(true, false, null);

        public static LinkTargetProbe Failed => new(false, false, null);
    }
}
