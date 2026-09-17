using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Microsoft.Win32.SafeHandles;

namespace YaziDesktopHost;

internal sealed record HostSettings(
    AppThemeMode ThemeMode,
    string FontFamily,
    int FontSize,
    ThemeColorOverrides? DarkColors = null,
    ThemeColorOverrides? LightColors = null,
    WindowPlacementSettings? WindowPlacement = null)
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

    public static bool TryNormalizeFontFamily(string? value, out string fontFamily)
    {
        fontFamily = DefaultFontFamily;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            fontFamily = new FontFamily(value.Trim()).Source;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool IsValidFontSize(int value)
    {
        return value is > 0 and <= short.MaxValue;
    }
}

internal enum HostSettingsLoadStatus
{
    Missing,
    Loaded,
    Failed,
}

internal sealed record HostSettingsLoadResult(
    HostSettings Settings,
    HostSettingsLoadStatus Status);

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
        return LoadWithStatus(GetPath()).Settings;
    }

    internal static HostSettings Load(string path)
    {
        return LoadWithStatus(path).Settings;
    }

    internal static bool TryLoad(string path, out HostSettings settings)
    {
        var result = LoadWithStatus(path);
        settings = result.Settings;
        return result.Status == HostSettingsLoadStatus.Loaded;
    }

    internal static HostSettingsLoadResult LoadWithStatus(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var persisted = JsonSerializer.Deserialize<PersistedHostSettings>(stream, SerializerOptions);
            if (persisted is null)
            {
                return new(HostSettings.Defaults, HostSettingsLoadStatus.Failed);
            }

            var themeMode = string.Equals(persisted.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                ? AppThemeMode.Light
                : AppThemeMode.Dark;
            var hasValidFontFamily = HostSettingsCatalog.TryNormalizeFontFamily(
                persisted.FontFamily,
                out var fontFamily);
            if (persisted.FontFamily is not null && !hasValidFontFamily)
            {
                AppLogger.Log("settings_font_family_fallback");
            }

            var requestedFontSize = persisted.FontSize;
            var hasValidFontSize = requestedFontSize is { } size
                && HostSettingsCatalog.IsValidFontSize(size);
            var fontSize = hasValidFontSize
                ? requestedFontSize!.Value
                : HostSettingsCatalog.DefaultFontSize;
            if (requestedFontSize is not null && !hasValidFontSize)
            {
                AppLogger.Log("settings_font_size_fallback");
            }

            var settings = new HostSettings(
                themeMode,
                fontFamily,
                fontSize,
                ParseOverrides(persisted.ThemeColors?.Dark),
                ParseOverrides(persisted.ThemeColors?.Light),
                ParseWindowPlacement(persisted.WindowPlacement));
            return new(settings, HostSettingsLoadStatus.Loaded);
        }
        catch (FileNotFoundException)
        {
            return new(HostSettings.Defaults, HostSettingsLoadStatus.Missing);
        }
        catch (DirectoryNotFoundException)
        {
            return new(HostSettings.Defaults, HostSettingsLoadStatus.Missing);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or NotSupportedException)
        {
            AppLogger.Log("settings_load_failed", exception);
            return new(HostSettings.Defaults, HostSettingsLoadStatus.Failed);
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
            var writePath = ResolveWritePath(path);
            var directory = Path.GetDirectoryName(writePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Settings path must include a directory.", nameof(path));
            }

            Directory.CreateDirectory(directory);
            var persisted = new PersistedHostSettings(
                settings.ThemeMode == AppThemeMode.Light ? "Light" : "Dark",
                settings.FontFamily,
                settings.FontSize,
                SerializeThemeColors(settings.DarkColors, settings.LightColors),
                SerializeWindowPlacement(settings.WindowPlacement));
            var serialized = JsonSerializer.Serialize(persisted, SerializerOptions);
            if (File.Exists(writePath)
                && TryGetHardLinkCount(writePath, out var hardLinkCount)
                && hardLinkCount > 1)
            {
                // Replacing the directory entry would split every other hard
                // link from this settings file. Preserve the file record when
                // Windows reports that it is shared by multiple links.
                File.WriteAllText(writePath, serialized);
                return;
            }

            var temporaryPath = $"{writePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, serialized);
                File.Move(temporaryPath, writePath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // The original settings file is already safe if cleanup fails.
                }
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            AppLogger.Log("settings_save_failed", exception);
        }
    }

    internal static string ResolveWritePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var current = Path.GetFullPath(path);
        for (var depth = 0; depth < 40; depth++)
        {
            var fileInfo = new FileInfo(current);
            var linkTarget = fileInfo.LinkTarget;
            if (linkTarget is null)
            {
                return current;
            }

            var resolved = fileInfo.ResolveLinkTarget(returnFinalTarget: false);
            if (resolved is null)
            {
                throw new IOException("The settings file link target could not be resolved.");
            }

            current = resolved.FullName;
        }

        throw new IOException("The settings file contains too many link levels.");
    }

    internal static bool TryGetHardLinkCount(string path, out uint count)
    {
        count = 0;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out var information))
            {
                return false;
            }

            count = information.NumberOfLinks;
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return false;
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
        PersistedThemeColors? ThemeColors = null,
        JsonElement? WindowPlacement = null);

    private sealed record PersistedWindowPlacement(
        string? LastMonitorId,
        PersistedMonitorWindowPlacement[]? Monitors);

    private sealed record PersistedMonitorWindowPlacement(
        string? MonitorId,
        int? Left,
        int? Top,
        int? Right,
        int? Bottom,
        string? State);

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

    private static WindowPlacementSettings? ParseWindowPlacement(JsonElement? value)
    {
        if (value is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            var persisted = element.Deserialize<PersistedWindowPlacement>(SerializerOptions);
            if (persisted?.Monitors is null)
            {
                return null;
            }

            var placements = persisted.Monitors
                .Where(item => item is not null
                    && item.MonitorId is not null
                    && item.Left is not null
                    && item.Top is not null
                    && item.Right is not null
                    && item.Bottom is not null
                    && (string.Equals(item.State, "Normal", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(item.State, "Maximized", StringComparison.OrdinalIgnoreCase)))
                .Select(item => new MonitorWindowPlacement(
                    item.MonitorId!,
                    new WindowBounds(
                        item.Left!.Value,
                        item.Top!.Value,
                        item.Right!.Value,
                        item.Bottom!.Value),
                    string.Equals(item.State, "Maximized", StringComparison.OrdinalIgnoreCase)
                        ? WindowPlacementShowState.Maximized
                        : WindowPlacementShowState.Normal));
            return WindowPlacementCatalog.Create(persisted.LastMonitorId, placements);
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static JsonElement? SerializeWindowPlacement(WindowPlacementSettings? settings)
    {
        if (settings is null)
        {
            return null;
        }

        var persisted = new PersistedWindowPlacement(
            settings.LastMonitorId,
            settings.Monitors
                .Select(placement => new PersistedMonitorWindowPlacement(
                    placement.MonitorId,
                    placement.NormalBounds.Left,
                    placement.NormalBounds.Top,
                    placement.NormalBounds.Right,
                    placement.NormalBounds.Bottom,
                    placement.ShowState == WindowPlacementShowState.Maximized
                        ? "Maximized"
                        : "Normal"))
                .ToArray());
        return JsonSerializer.SerializeToElement(persisted, SerializerOptions);
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
