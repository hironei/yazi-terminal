using System.ComponentModel;
using System.Windows;
using VirtualTerminal;

namespace YaziDesktopHost;

public partial class MainWindow : Window
{
    private CommandLineSession? _session;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_session is not null)
        {
            return;
        }

        try
        {
            var executable = YaziExecutableResolver.Resolve();
            _session = new CommandLineSession(executable);
            Terminal.Session = _session;
            Terminal.Focus();
        }
        catch (YaziExecutableNotFoundException)
        {
            ShowStartupError("yazi.exe was not found. Set YAZI_PATH or add yazi.exe to PATH.");
        }
        catch (Exception exception)
        {
            AppLogger.Log("yazi_start_failed", exception);
            ShowStartupError("Yazi could not be started. See the application log for details.");
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        Terminal.Session = null;
        _session?.Dispose();
        _session = null;
    }

    private void ShowStartupError(string message)
    {
        AppLogger.Log("yazi_start_unavailable");
        MessageBox.Show(this, message, "Yazi Desktop Host", MessageBoxButton.OK, MessageBoxImage.Error);
        Close();
    }
}
