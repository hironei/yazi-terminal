namespace YaziDesktopHost;

internal enum PaletteCommandId
{
    DarkTheme,
    LightTheme,
    EditSettings,
    YaziAction,
}

internal readonly record struct CommandPaletteCommand(
    PaletteCommandId Id,
    string Title,
    string Description,
    YaziBridgeCommand? YaziCommand = null)
{
    public AppThemeMode ThemeMode => Id switch
    {
        PaletteCommandId.DarkTheme => AppThemeMode.Dark,
        PaletteCommandId.LightTheme => AppThemeMode.Light,
        _ => throw new ArgumentOutOfRangeException(nameof(Id), Id, null),
    };
}

internal static class CommandPaletteCommands
{
    private static readonly CommandPaletteCommand[] ThemeCommands =
    [
        new(
            PaletteCommandId.DarkTheme,
            "Theme: Dark",
            "Use a dark host and terminal palette"),
        new(
            PaletteCommandId.LightTheme,
            "Theme: Light",
            "Use a high-contrast light host and terminal palette"),
        new(
            PaletteCommandId.EditSettings,
            "Settings: Edit terminal appearance",
            "Open settings.json in Yazi's configured editor"),
    ];

    public static IReadOnlyList<CommandPaletteCommand> All => ThemeCommands;

    public static IReadOnlyList<CommandPaletteCommand> WithYaziCommands(
        IReadOnlyList<YaziBridgeCommand> yaziCommands)
    {
        ArgumentNullException.ThrowIfNull(yaziCommands);
        if (yaziCommands.Count == 0)
        {
            return ThemeCommands;
        }

        var commands = new List<CommandPaletteCommand>(ThemeCommands.Length + yaziCommands.Count);
        commands.AddRange(ThemeCommands);
        commands.AddRange(yaziCommands.Select(command =>
        {
            var title = string.IsNullOrWhiteSpace(command.Description)
                ? $"Yazi: {command.Run}"
                : $"Yazi: {command.Description}";
            var key = string.IsNullOrWhiteSpace(command.Key) ? "No key" : command.Key;
            var description = $"{key}  ·  {command.Run}";
            return new CommandPaletteCommand(PaletteCommandId.YaziAction, title, description, command);
        }));
        return commands;
    }

    public static IReadOnlyList<CommandPaletteCommand> Filter(
        IEnumerable<CommandPaletteCommand> commands,
        string? query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
        {
            return commands.ToArray();
        }

        return commands
            .Where(command =>
                command.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || command.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || command.Id.ToString().Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}

internal enum PaletteNavigationKey
{
    Up,
    Down,
    J,
    K,
    Other,
}

internal static class PaletteNavigation
{
    public static int? TryGetMoveOffset(
        PaletteNavigationKey key,
        bool hasNoModifiers,
        string? query)
    {
        return key switch
        {
            PaletteNavigationKey.Down => 1,
            PaletteNavigationKey.Up => -1,
            PaletteNavigationKey.J when hasNoModifiers && string.IsNullOrWhiteSpace(query) => 1,
            PaletteNavigationKey.K when hasNoModifiers && string.IsNullOrWhiteSpace(query) => -1,
            _ => null,
        };
    }

    public static int NextIndex(int itemCount, int selectedIndex, int offset)
    {
        if (itemCount <= 0)
        {
            return -1;
        }

        if (offset == 0)
        {
            return selectedIndex >= 0 && selectedIndex < itemCount ? selectedIndex : -1;
        }

        var currentIndex = selectedIndex >= 0 && selectedIndex < itemCount
            ? selectedIndex
            : offset > 0 ? -1 : 0;
        var nextIndex = (currentIndex + offset) % itemCount;
        return nextIndex < 0 ? nextIndex + itemCount : nextIndex;
    }
}
