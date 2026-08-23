using System.IO;
using System.Text.Json;

namespace YaziDesktopHost;

internal sealed record HostSettings(
    AppThemeMode ThemeMode,
    string FontFamily,
    int FontSize)
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
            var fontFamily = HostSettingsCatalog.IsSupportedFontFamily(persisted.FontFamily)
                ? HostSettingsCatalog.FontFamilies.First(
                    candidate => string.Equals(candidate, persisted.FontFamily, StringComparison.OrdinalIgnoreCase))
                : HostSettingsCatalog.DefaultFontFamily;
            var fontSize = persisted.FontSize is { } size
                && HostSettingsCatalog.IsSupportedFontSize(size)
                ? size
                : HostSettingsCatalog.DefaultFontSize;

            settings = new HostSettings(themeMode, fontFamily, fontSize);
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
                settings.FontSize);
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
        int? FontSize);
}
