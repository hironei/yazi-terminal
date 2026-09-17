using System.Windows;
using System.Windows.Threading;

namespace YaziDesktopHost;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private int _unhandledExceptionReported;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
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
                    TimeSpan.FromSeconds(6)))
            {
                Shutdown();
                return;
            }
        }

        var window = new MainWindow(options.InitialDirectory, options.FilePath);
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Log("dispatcher_unhandled_exception", e.Exception);
        e.Handled = true;
        if (Interlocked.Exchange(ref _unhandledExceptionReported, 1) == 0)
        {
            MessageBox.Show(
                "Yazi Terminal encountered an unexpected error and will close. See the application log for details.",
                "Yazi Terminal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        Shutdown(1);
    }

    private static void OnAppDomainUnhandledException(
        object? sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            AppLogger.Log("appdomain_unhandled_exception", exception);
        }
        else
        {
            AppLogger.Log("appdomain_unhandled_exception");
        }
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Log("unobserved_task_exception", e.Exception);
        e.SetObserved();
    }
}
