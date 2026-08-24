using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace YaziDesktopHost;

internal partial class CommandPaletteWindow : Window
{
    private readonly IReadOnlyList<CommandPaletteCommand> _commands;

    internal CommandPaletteWindow(
        ThemeColors colors,
        IReadOnlyList<CommandPaletteCommand> commands)
    {
        _commands = commands;
        InitializeComponent();
        ApplyTheme(colors);
        RefreshCommands();
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        };
    }

    internal CommandPaletteCommand? SelectedCommand { get; private set; }

    private void ApplyTheme(ThemeColors colors)
    {
        Resources["PaletteBackgroundBrush"] = CreateBrush(colors.PaletteBackground);
        Resources["PaletteForegroundBrush"] = CreateBrush(colors.PaletteForeground);
        Resources["PaletteBorderBrush"] = CreateBrush(colors.PaletteBorder);
        Resources["PaletteInputBackgroundBrush"] = CreateBrush(colors.PaletteInputBackground);
        Resources["PaletteSelectionBackgroundBrush"] = CreateBrush(colors.PaletteSelectionBackground);
        Resources["PaletteSelectionForegroundBrush"] = CreateBrush(colors.PaletteSelectionForeground);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshCommands();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        HandleNavigationKey(e);
    }

    private void HandleNavigationKey(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ExecuteSelectedCommand();
            e.Handled = true;
            return;
        }

        var navigationKey = e.Key switch
        {
            Key.Down => PaletteNavigationKey.Down,
            Key.Up => PaletteNavigationKey.Up,
            Key.J => PaletteNavigationKey.J,
            Key.K => PaletteNavigationKey.K,
            _ => PaletteNavigationKey.Other,
        };
        if (PaletteNavigation.TryGetMoveOffset(
                navigationKey,
                Keyboard.Modifiers == ModifierKeys.None,
                SearchBox.Text) is int offset)
        {
            MoveSelection(offset);
            e.Handled = true;
        }
    }

    private void CommandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ExecuteSelectedCommand();
        e.Handled = true;
    }

    private void RefreshCommands()
    {
        var filtered = CommandPaletteCommands.Filter(_commands, SearchBox.Text);
        CommandList.ItemsSource = filtered;
        CommandList.SelectedIndex = filtered.Count == 0 ? -1 : 0;
        EmptyText.Visibility = filtered.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MoveSelection(int offset)
    {
        var nextIndex = PaletteNavigation.NextIndex(
            CommandList.Items.Count,
            CommandList.SelectedIndex,
            offset);
        if (nextIndex < 0)
        {
            return;
        }

        CommandList.SelectedIndex = nextIndex;
        CommandList.ScrollIntoView(CommandList.SelectedItem);
    }

    private void ExecuteSelectedCommand()
    {
        if (CommandList.SelectedItem is not CommandPaletteCommand command)
        {
            return;
        }

        SelectedCommand = command;
        DialogResult = true;
    }

    private static SolidColorBrush CreateBrush(RgbColor color)
    {
        return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
    }
}
