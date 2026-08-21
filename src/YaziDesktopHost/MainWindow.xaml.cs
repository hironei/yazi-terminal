using System.ComponentModel;
using System.Windows;
using VirtualTerminal;

namespace YaziDesktopHost;

public partial class MainWindow : Window
{
    private CommandLineSession? _session;
    private YaziBridgePipeServer? _bridgeServer;
    private YaziBridgeSession? _bridgeSession;
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
            var instanceId = Guid.NewGuid();
            _bridgeServer = new YaziBridgePipeServer(instanceId);
            _bridgeSession = new YaziBridgeSession(instanceId, _bridgeServer);
            _bridgeSession.Disconnected += Bridge_Disconnected;
            _ = _bridgeSession.RunAsync();

            _session = YaziProcessCreationInfoFactory.StartSession(
                executable,
                Environment.CurrentDirectory,
                instanceId,
                _bridgeServer.PipePath);
            _session.Disconnected += Session_Disconnected;
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
        DisposeSession();
    }

    private void Session_Disconnected(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isClosing)
            {
                return;
            }

            AppLogger.Log("yazi_session_disconnected");
            _isClosing = true;
            DisposeSession();
            MessageBox.Show(
                this,
                "Yazi stopped unexpectedly. See the application log for details.",
                "Yazi Desktop Host",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }));
    }

    private void DisposeSession()
    {
        DisposeBridge();

        if (_session is null)
        {
            Terminal.Session = null;
            return;
        }

        _session.Disconnected -= Session_Disconnected;
        Terminal.Session = null;
        _session.Dispose();
        _session = null;
    }

    private void DisposeBridge()
    {
        if (_bridgeSession is null)
        {
            _bridgeServer?.Dispose();
            _bridgeServer = null;
            return;
        }

        _bridgeSession.Disconnected -= Bridge_Disconnected;
        _bridgeSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _bridgeSession = null;
        _bridgeServer = null;
    }

    private void Bridge_Disconnected(string reason)
    {
        if (!_isClosing)
        {
            AppLogger.Log($"yazi_bridge_disconnected_{reason}");
        }
    }

    private void ShowStartupError(string message)
    {
        AppLogger.Log("yazi_start_unavailable");
        MessageBox.Show(this, message, "Yazi Desktop Host", MessageBoxButton.OK, MessageBoxImage.Error);
        Close();
    }
}
