namespace YaziDesktopHost;

internal enum AppThemeMode
{
    Dark,
    Light,
}

internal readonly record struct RgbColor(byte Red, byte Green, byte Blue);

internal sealed record ThemeColors(
    RgbColor HostBackground,
    RgbColor HostForeground,
    RgbColor MenuBorder,
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
        new(0, 0, 0),
        new(160, 0, 0),
        new(0, 128, 0),
        new(128, 128, 0),
        new(0, 0, 160),
        new(128, 0, 128),
        new(0, 128, 128),
        new(128, 128, 128),
        new(96, 96, 96),
        new(0, 0, 255),
        new(0, 170, 0),
        new(170, 120, 0),
        new(220, 0, 0),
        new(220, 0, 220),
        new(0, 150, 170),
        new(64, 64, 64),
    ];

    public static ThemeColors For(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Dark => new(
            new(0, 0, 0),
            new(255, 255, 255),
            new(64, 64, 64),
            new(0, 0, 0),
            new(255, 255, 255),
            new(30, 90, 160),
            DarkColorTable),
        AppThemeMode.Light => new(
            new(245, 245, 245),
            new(31, 31, 31),
            new(200, 200, 200),
            new(251, 251, 251),
            new(31, 31, 31),
            new(184, 215, 255),
            LightColorTable),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
