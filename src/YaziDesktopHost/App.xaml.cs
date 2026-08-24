using System.Configuration;
using System.Data;
using System.Windows;

namespace YaziDesktopHost;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(e.Args, Environment.CurrentDirectory);
        }
        catch (CommandLineParseException exception)
        {
            AppLogger.Log("command_line_invalid", exception);
            MessageBox.Show(
                exception.Message,
                "Yazi Terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        if (options.UseLastInstance)
        {
            var registry = new LastInstanceRegistry();
            if (registry.TryRead(out var endpoint)
                && endpoint is not null
                && LastInstanceClient.TrySend(
                    endpoint,
                    new LastInstanceControlRequest(
                        options.FilePath ?? options.InitialDirectory,
                        options.FilePath is null
                            ? LastInstanceControlCommand.ChangeDirectory
                            : LastInstanceControlCommand.OpenFile),
                    TimeSpan.FromSeconds(2)))
            {
                Shutdown();
                return;
            }
        }

        var window = new MainWindow(options.InitialDirectory, options.FilePath);
        MainWindow = window;
        window.Show();
    }
}
