using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YaziDesktopHost;

internal sealed record HostSettings(
    AppThemeMode ThemeMode,
    string FontFamily,
    int FontSize,
    ThemeColorOverrides? DarkColors = null,
    ThemeColorOverrides? LightColors = null)
{
    public static HostSettings Defaults => new(
        AppThemeMode.Dark,
        HostSettingsCatalog.DefaultFontFamily,
        HostSettingsCatalog.DefaultFontSize);
}

internal static class HostSettingsCatalog
{
    public const string DefaultFontFamily = "MS Gothic";
    public const int DefaultFontSize = 14;

    public static IReadOnlyList<string> FontFamilies { get; } =
    [
        "MS Gothic",
        "Consolas",
        "Cascadia Mono",
        "Cascadia Code",
    ];

    public static IReadOnlyList<int> FontSizes { get; } = [12, 14, 16, 18, 20];

    public static bool IsSupportedFontFamily(string? value)
    {
        return value is not null
            && FontFamilies.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsSupportedFontSize(int value)
    {
        return FontSizes.Contains(value);
    }
}

internal static class HostSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static HostSettings Load()
    {
        return TryLoad(GetPath(), out var settings)
            ? settings
            : HostSettings.Defaults;
    }

    internal static HostSettings Load(string path)
    {
        return TryLoad(path, out var settings)
            ? settings
            : HostSettings.Defaults;
    }

    internal static bool TryLoad(string path, out HostSettings settings)
    {
        settings = HostSettings.Defaults;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var persisted = JsonSerializer.Deserialize<PersistedHostSettings>(
                File.ReadAllText(path),
                SerializerOptions);
            if (persisted is null)
            {
                return false;
            }

            var themeMode = string.Equals(persisted.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                ? AppThemeMode.Light
                : AppThemeMode.Dark;
            var hasSupportedFontFamily = HostSettingsCatalog.IsSupportedFontFamily(persisted.FontFamily);
            var fontFamily = hasSupportedFontFamily
                ? HostSettingsCatalog.FontFamilies.First(
                    candidate => string.Equals(candidate, persisted.FontFamily, StringComparison.OrdinalIgnoreCase))
                : HostSettingsCatalog.DefaultFontFamily;
            if (persisted.FontFamily is not null && !hasSupportedFontFamily)
            {
                AppLogger.Log("settings_font_family_fallback");
            }

            var requestedFontSize = persisted.FontSize;
            var hasSupportedFontSize = requestedFontSize is { } size
                && HostSettingsCatalog.IsSupportedFontSize(size);
            var fontSize = hasSupportedFontSize
                ? requestedFontSize!.Value
                : HostSettingsCatalog.DefaultFontSize;
            if (requestedFontSize is not null && !hasSupportedFontSize)
            {
                AppLogger.Log("settings_font_size_fallback");
            }

            settings = new HostSettings(
                themeMode,
                fontFamily,
                fontSize,
                ParseOverrides(persisted.ThemeColors?.Dark),
                ParseOverrides(persisted.ThemeColors?.Light));
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or NotSupportedException)
        {
            AppLogger.Log("settings_load_failed", exception);
            return false;
        }
    }

    public static void Save(HostSettings settings)
    {
        Save(settings, GetPath());
    }

    internal static void Save(HostSettings settings, string path)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Settings path must include a directory.", nameof(path));
            }

            Directory.CreateDirectory(directory);
            var persisted = new PersistedHostSettings(
                settings.ThemeMode == AppThemeMode.Light ? "Light" : "Dark",
                settings.FontFamily,
                settings.FontSize,
                SerializeThemeColors(settings.DarkColors, settings.LightColors));
            File.WriteAllText(path, JsonSerializer.Serialize(persisted, SerializerOptions));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            AppLogger.Log("settings_save_failed", exception);
        }
    }

    internal static string GetPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YaziTerminal",
            "settings.json");
    }

    private sealed record PersistedHostSettings(
        string? Theme,
        string? FontFamily,
        int? FontSize,
        PersistedThemeColors? ThemeColors = null);

    private sealed record PersistedThemeColors(
        PersistedThemeColorOverrides? Dark,
        PersistedThemeColorOverrides? Light);

    private sealed record PersistedThemeColorOverrides(
        JsonElement? HostBackground = null,
        JsonElement? HostForeground = null,
        JsonElement? PaletteBackground = null,
        JsonElement? PaletteForeground = null,
        JsonElement? PaletteBorder = null,
        JsonElement? PaletteInputBackground = null,
        JsonElement? PaletteSelectionBackground = null,
        JsonElement? PaletteSelectionForeground = null,
        JsonElement? TerminalBackground = null,
        JsonElement? TerminalForeground = null,
        JsonElement? TerminalSelectionBackground = null,
        JsonElement? TerminalColorTable = null);

    private static ThemeColorOverrides? ParseOverrides(PersistedThemeColorOverrides? persisted)
    {
        if (persisted is null)
        {
            return null;
        }

        return new ThemeColorOverrides(
            ParseColor(persisted.HostBackground),
            ParseColor(persisted.HostForeground),
            ParseColor(persisted.PaletteBackground),
            ParseColor(persisted.PaletteForeground),
            ParseColor(persisted.PaletteBorder),
            ParseColor(persisted.PaletteInputBackground),
            ParseColor(persisted.PaletteSelectionBackground),
            ParseColor(persisted.PaletteSelectionForeground),
            ParseColor(persisted.TerminalBackground),
            ParseColor(persisted.TerminalForeground),
            ParseColor(persisted.TerminalSelectionBackground),
            ParseColorTable(persisted.TerminalColorTable));
    }

    private static RgbColor? ParseColor(JsonElement? value)
    {
        if (value is not JsonElement element || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return RgbColor.TryParse(element.GetString(), out var color) ? color : null;
    }

    private static IReadOnlyList<RgbColor>? ParseColorTable(JsonElement? value)
    {
        if (value is not JsonElement element || element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var colors = new List<RgbColor>();
        foreach (var item in element.EnumerateArray())
        {
            var color = ParseColor(item);
            if (color is null)
            {
                return null;
            }

            colors.Add(color.Value);
        }

        return colors.Count == 16 ? colors : null;
    }

    private static PersistedThemeColors? SerializeThemeColors(
        ThemeColorOverrides? dark,
        ThemeColorOverrides? light)
    {
        if (dark is null && light is null)
        {
            return null;
        }

        return new PersistedThemeColors(
            SerializeOverrides(dark),
            SerializeOverrides(light));
    }

    private static PersistedThemeColorOverrides? SerializeOverrides(ThemeColorOverrides? overrides)
    {
        if (overrides is null)
        {
            return null;
        }

        return new PersistedThemeColorOverrides(
            FormatColor(overrides.HostBackground),
            FormatColor(overrides.HostForeground),
            FormatColor(overrides.PaletteBackground),
            FormatColor(overrides.PaletteForeground),
            FormatColor(overrides.PaletteBorder),
            FormatColor(overrides.PaletteInputBackground),
            FormatColor(overrides.PaletteSelectionBackground),
            FormatColor(overrides.PaletteSelectionForeground),
            FormatColor(overrides.TerminalBackground),
            FormatColor(overrides.TerminalForeground),
            FormatColor(overrides.TerminalSelectionBackground),
            overrides.TerminalColorTable is { } table
                ? JsonSerializer.SerializeToElement(table.Select(color => color.ToHex()).ToArray(), SerializerOptions)
                : null);
    }

    private static JsonElement? FormatColor(RgbColor? color)
    {
        return color is { } value
            ? JsonSerializer.SerializeToElement(value.ToHex(), SerializerOptions)
            : null;
    }
}
