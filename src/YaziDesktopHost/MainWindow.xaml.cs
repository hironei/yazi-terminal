using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using EasyWindowsTerminalControl;
using Microsoft.Terminal.Wpf;

namespace YaziDesktopHost;

public partial class MainWindow : Window
{
    private EasyTerminalControl? _terminal;
    private TermPTY? _term;
    private YaziBridgePipeServer? _bridgeServer;
    private YaziBridgeSession? _bridgeSession;
    private IDisposable? _bridgeEnvironment;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_terminal is not null)
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

            _bridgeEnvironment = YaziProcessLaunchConfiguration.EnterBridgeEnvironment(
                instanceId,
                _bridgeServer.PipePath);
            _term = new TermPTY();
            _terminal = new EasyTerminalControl
            {
                StartupCommandLine = YaziProcessLaunchConfiguration.CreateCommandLine(executable),
                WorkingDirectory = Environment.CurrentDirectory,
                ConPTYTerm = _term,
                Theme = CreateTerminalTheme(),
                FontFamilyWhenSettingTheme = new FontFamily("MS Gothic"),
                FontSizeWhenSettingTheme = 14,
                Win32InputMode = true,
            };
            TerminalHost.Children.Add(_terminal);
            _terminal.Focus();
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

    private void DisposeSession()
    {
        _terminal?.DisconnectConPTYTerm();
        _term?.CloseStdinToApp();
        _term?.StopExternalTermOnly();
        _terminal = null;
        _term = null;
        TerminalHost.Children.Clear();
        _bridgeEnvironment?.Dispose();
        _bridgeEnvironment = null;
        DisposeBridge();
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

    private static TerminalTheme CreateTerminalTheme()
    {
        return new TerminalTheme
        {
            DefaultBackground = Rgb(0, 0, 0),
            DefaultForeground = Rgb(255, 255, 255),
            DefaultSelectionBackground = Rgb(30, 90, 160),
            CursorStyle = CursorStyle.SteadyBlock,
            ColorTable =
            [
                Rgb(0, 0, 0),
                Rgb(0, 0, 128),
                Rgb(0, 128, 0),
                Rgb(0, 128, 128),
                Rgb(128, 0, 0),
                Rgb(128, 0, 128),
                Rgb(128, 128, 0),
                Rgb(192, 192, 192),
                Rgb(128, 128, 128),
                Rgb(0, 0, 255),
                Rgb(0, 255, 0),
                Rgb(0, 255, 255),
                Rgb(255, 0, 0),
                Rgb(255, 0, 255),
                Rgb(255, 255, 0),
                Rgb(255, 255, 255),
            ],
        };
    }

    private static uint Rgb(byte red, byte green, byte blue)
    {
        return EasyTerminalControl.ColorToVal(Color.FromRgb(red, green, blue));
    }
}
