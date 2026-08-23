using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YaziDesktopHost;

internal sealed record YaziThemeColors(
    RgbColor? Foreground,
    RgbColor? Border,
    RgbColor? SelectionBackground,
    RgbColor? SelectionForeground,
    string? FlavorName = null,
    RgbColor? FileForeground = null,
    RgbColor? DirectoryForeground = null,
    RgbColor? TerminalBackground = null);

internal static class YaziThemeLoader
{
    private static readonly Regex FlavorSelectionPattern = new(
        "^\\s*(light|dark)\\s*=\\s*[\\\"']([^\\\"']+)[\\\"']",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ColorPattern = new(
        "(?<name>fg|bg)\\s*=\\s*[\\\"']#(?<value>[0-9a-fA-F]{6})[\\\"']",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static YaziThemeColors? Load(AppThemeMode mode)
    {
        var configHome = ResolveConfigHome();
        return configHome is null ? null : Load(mode, configHome);
    }

    internal static YaziThemeColors? Load(AppThemeMode mode, string configHome)
    {
        try
        {
            var themePath = Path.Combine(configHome, "theme.toml");
            if (!File.Exists(themePath))
            {
                return null;
            }

            var flavorName = ReadFlavorName(File.ReadAllLines(themePath), mode);
            if (string.IsNullOrWhiteSpace(flavorName))
            {
                return null;
            }

            var flavorPath = Path.Combine(configHome, "flavors", $"{flavorName}.yazi", "flavor.toml");
            if (!File.Exists(flavorPath))
            {
                return null;
            }

            return ReadFlavor(File.ReadAllLines(flavorPath), flavorName);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return null;
        }
    }

    internal static string? ResolveConfigHome()
    {
        var configuredHome = Environment.GetEnvironmentVariable("YAZI_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(configuredHome))
        {
            return configuredHome;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(appData)
            ? null
            : Path.Combine(appData, "yazi", "config");
    }

    private static string? ReadFlavorName(IEnumerable<string> lines, AppThemeMode mode)
    {
        var expectedName = mode == AppThemeMode.Dark ? "dark" : "light";
        var inFlavorSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                inFlavorSection = string.Equals(trimmed, "[flavor]", StringComparison.Ordinal);
                continue;
            }

            if (!inFlavorSection)
            {
                continue;
            }

            var match = FlavorSelectionPattern.Match(trimmed);
            if (match.Success
                && string.Equals(match.Groups[1].Value, expectedName, StringComparison.Ordinal))
            {
                return match.Groups[2].Value.Trim();
            }
        }

        return null;
    }

    private static YaziThemeColors ReadFlavor(IEnumerable<string> lines, string flavorName)
    {
        var section = string.Empty;
        RgbColor? foreground = null;
        RgbColor? fallbackForeground = null;
        RgbColor? directoryForeground = null;
        RgbColor? terminalBackground = null;
        RgbColor? border = null;
        RgbColor? selectionBackground = null;
        RgbColor? selectionForeground = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                section = trimmed[1..^1];
                continue;
            }

            var colors = ExtractColors(trimmed);
            if (colors is null)
            {
                continue;
            }

            if (section == "mgr" && trimmed.StartsWith("cwd", StringComparison.Ordinal))
            {
                foreground ??= colors.Foreground;
            }
            else if (section == "mgr" && trimmed.StartsWith("border_style", StringComparison.Ordinal))
            {
                border ??= colors.Foreground;
            }
            else if (section == "tabs" && trimmed.StartsWith("active", StringComparison.Ordinal))
            {
                selectionBackground ??= colors.Background;
                selectionForeground ??= colors.Foreground;
            }
            else if (section == "app"
                && trimmed.StartsWith("overall", StringComparison.Ordinal))
            {
                terminalBackground ??= colors.Background;
            }
            else if (section == "filetype"
                && ContainsUrlRule(trimmed, "*"))
            {
                fallbackForeground ??= colors.Foreground;
            }
            else if (section == "filetype"
                && ContainsUrlRule(trimmed, "*/"))
            {
                directoryForeground ??= colors.Foreground;
            }
        }

        return new YaziThemeColors(
            fallbackForeground ?? foreground,
            border,
            selectionBackground,
            selectionForeground,
            flavorName,
            fallbackForeground,
            directoryForeground,
            terminalBackground);
    }

    private static bool ContainsUrlRule(string line, string value)
    {
        return line.Contains($"url = \"{value}\"", StringComparison.Ordinal)
            || line.Contains($"url=\"{value}\"", StringComparison.Ordinal)
            || line.Contains($"url = '{value}'", StringComparison.Ordinal)
            || line.Contains($"url='{value}'", StringComparison.Ordinal);
    }

    private static ParsedColors? ExtractColors(string line)
    {
        var matches = ColorPattern.Matches(line);
        if (matches.Count == 0)
        {
            return null;
        }

        RgbColor? foreground = null;
        RgbColor? background = null;
        foreach (Match match in matches)
        {
            var value = match.Groups["value"].Value;
            if (!byte.TryParse(value[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
                || !byte.TryParse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
                || !byte.TryParse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            {
                continue;
            }

            var color = new RgbColor(red, green, blue);
            if (match.Groups["name"].Value == "fg")
            {
                foreground = color;
            }
            else
            {
                background = color;
            }
        }

        return new ParsedColors(foreground, background);
    }

    private sealed record ParsedColors(RgbColor? Foreground, RgbColor? Background);
}
