using System.Globalization;

namespace YaziDesktopHost;

internal enum AppThemeMode
{
    Dark,
    Light,
}

internal readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    internal static bool TryParse(string? value, out RgbColor color)
    {
        color = default;
        if (value is null || value.Length != 7 || value[0] != '#'
            || !byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
            || !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
            || !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return false;
        }

        color = new RgbColor(red, green, blue);
        return true;
    }

    internal string ToHex() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}

internal sealed record ThemeColorOverrides(
    RgbColor? HostBackground = null,
    RgbColor? HostForeground = null,
    RgbColor? PaletteBackground = null,
    RgbColor? PaletteForeground = null,
    RgbColor? PaletteBorder = null,
    RgbColor? PaletteInputBackground = null,
    RgbColor? PaletteSelectionBackground = null,
    RgbColor? PaletteSelectionForeground = null,
    RgbColor? TerminalBackground = null,
    RgbColor? TerminalForeground = null,
    RgbColor? TerminalSelectionBackground = null,
    IReadOnlyList<RgbColor>? TerminalColorTable = null);

internal sealed record ThemeColors(
    RgbColor HostBackground,
    RgbColor HostForeground,
    RgbColor PaletteBackground,
    RgbColor PaletteForeground,
    RgbColor PaletteBorder,
    RgbColor PaletteInputBackground,
    RgbColor PaletteSelectionBackground,
    RgbColor PaletteSelectionForeground,
    RgbColor TerminalBackground,
    RgbColor TerminalForeground,
    RgbColor TerminalSelectionBackground,
    IReadOnlyList<RgbColor> TerminalColorTable);

internal static class ThemePalette
{
    private static readonly RgbColor[] DarkColorTable =
    [
        new(0, 0, 0),
        new(0, 0, 128),
        new(0, 128, 0),
        new(0, 128, 128),
        new(128, 0, 0),
        new(128, 0, 128),
        new(128, 128, 0),
        new(192, 192, 192),
        new(128, 128, 128),
        new(0, 0, 255),
        new(0, 255, 0),
        new(0, 255, 255),
        new(255, 0, 0),
        new(255, 0, 255),
        new(255, 255, 0),
        new(255, 255, 255),
    ];

    private static readonly RgbColor[] LightColorTable =
    [
        new(7, 54, 66),
        new(220, 50, 47),
        new(133, 153, 0),
        new(181, 137, 0),
        new(38, 139, 210),
        new(211, 54, 130),
        new(42, 161, 152),
        new(238, 232, 213),
        new(0, 43, 54),
        new(203, 75, 22),
        new(88, 110, 117),
        new(101, 123, 131),
        new(131, 148, 150),
        new(108, 113, 196),
        new(147, 161, 161),
        new(253, 246, 227),
    ];

    public static ThemeColors For(
        AppThemeMode mode,
        YaziThemeColors? yaziTheme = null,
        ThemeColorOverrides? overrides = null)
    {
        var colors = mode switch
        {
            AppThemeMode.Dark => new ThemeColors(
                new(0, 0, 0),
                new(255, 255, 255),
                new(37, 37, 38),
                new(241, 241, 241),
                new(82, 82, 82),
                new(45, 45, 48),
                new(38, 79, 120),
                new(255, 255, 255),
                new(0, 0, 0),
                new(255, 255, 255),
                new(30, 90, 160),
                DarkColorTable),
            AppThemeMode.Light => new ThemeColors(
                new(238, 232, 213),
                new(7, 54, 66),
                new(253, 246, 227),
                new(7, 54, 66),
                new(147, 161, 161),
                new(238, 232, 213),
                new(38, 139, 210),
                new(253, 246, 227),
                new(253, 246, 227),
                new(7, 54, 66),
                new(238, 232, 213),
                LightColorTable),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        return ApplySettings(ApplyYaziTheme(colors, yaziTheme), overrides);
    }

    private static ThemeColors ApplySettings(ThemeColors colors, ThemeColorOverrides? overrides)
    {
        if (overrides is null)
        {
            return colors;
        }

        return colors with
        {
            HostBackground = overrides.HostBackground ?? colors.HostBackground,
            HostForeground = overrides.HostForeground ?? colors.HostForeground,
            PaletteBackground = overrides.PaletteBackground ?? colors.PaletteBackground,
            PaletteForeground = overrides.PaletteForeground ?? colors.PaletteForeground,
            PaletteBorder = overrides.PaletteBorder ?? colors.PaletteBorder,
            PaletteInputBackground = overrides.PaletteInputBackground ?? colors.PaletteInputBackground,
            PaletteSelectionBackground = overrides.PaletteSelectionBackground ?? colors.PaletteSelectionBackground,
            PaletteSelectionForeground = overrides.PaletteSelectionForeground ?? colors.PaletteSelectionForeground,
            TerminalBackground = overrides.TerminalBackground ?? colors.TerminalBackground,
            TerminalForeground = overrides.TerminalForeground ?? colors.TerminalForeground,
            TerminalSelectionBackground = overrides.TerminalSelectionBackground ?? colors.TerminalSelectionBackground,
            TerminalColorTable = overrides.TerminalColorTable is { Count: 16 } table
                ? table.ToArray()
                : colors.TerminalColorTable,
        };
    }

    private static ThemeColors ApplyYaziTheme(ThemeColors colors, YaziThemeColors? yaziTheme)
    {
        if (yaziTheme is null)
        {
            return colors;
        }

        return colors with
        {
            TerminalBackground = yaziTheme.TerminalBackground ?? colors.TerminalBackground,
            HostForeground = yaziTheme.Foreground ?? colors.HostForeground,
            PaletteForeground = yaziTheme.Foreground ?? colors.PaletteForeground,
            PaletteBorder = yaziTheme.Border ?? colors.PaletteBorder,
            PaletteSelectionBackground = yaziTheme.SelectionBackground ?? colors.PaletteSelectionBackground,
            PaletteSelectionForeground = yaziTheme.SelectionForeground ?? colors.PaletteSelectionForeground,
            TerminalForeground = yaziTheme.Foreground ?? colors.TerminalForeground,
        };
    }
}
