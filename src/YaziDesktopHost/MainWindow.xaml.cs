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
    private CancellationTokenSource? _processMonitorCancellation;
    private Task? _processMonitorTask;
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
            _term.TermReady += Term_TermReady;
            _term.TerminalOutput += Term_TerminalOutput;
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
        if (_term is not null)
        {
            _term.TermReady -= Term_TermReady;
            _term.TerminalOutput -= Term_TerminalOutput;
        }

        _processMonitorCancellation?.Cancel();
        _processMonitorCancellation?.Dispose();
        _processMonitorCancellation = null;
        _processMonitorTask = null;
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

    private void Term_TerminalOutput(object? sender, TerminalOutputEventArgs e)
    {
        if (sender is TermPTY term
            && string.Equals(e.Data, "Session Terminated", StringComparison.Ordinal))
        {
            Dispatcher.BeginInvoke(() => HandleUnexpectedExit(term));
        }
    }

    private void Term_TermReady(object? sender, EventArgs e)
    {
        if (_processMonitorTask is not null || sender is not TermPTY term)
        {
            return;
        }

        AppLogger.Log("yazi_process_monitor_starting");
        _processMonitorCancellation = new CancellationTokenSource();
        _processMonitorTask = MonitorProcessExitAsync(term, _processMonitorCancellation.Token);

        if (TerminalColorFixture.IsEnabled)
        {
            _ = ShowTerminalColorFixtureAsync(term, _processMonitorCancellation.Token);
        }
    }

    private static async Task ShowTerminalColorFixtureAsync(TermPTY term, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                term.WriteToUITerminal(TerminalColorFixture.Sequence);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal window shutdown.
        }
    }

    private Task MonitorProcessExitAsync(TermPTY term, CancellationToken cancellationToken)
    {
        var process = term.Process;
        if (process is null)
        {
            AppLogger.Log("yazi_exit_observation_unavailable");
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            try
            {
                process.WaitForExit();
                if (!cancellationToken.IsCancellationRequested)
                {
                    AppLogger.Log("yazi_process_exit_detected");
                    Dispatcher.Invoke(() => HandleUnexpectedExit(term));
                }
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    AppLogger.Log("yazi_exit_observation_failed", exception);
                    Dispatcher.Invoke(() => HandleUnexpectedExit(term));
                }
            }
        });
    }

    private void HandleUnexpectedExit(TermPTY term)
    {
        if (_isClosing || !ReferenceEquals(_term, term))
        {
            return;
        }

        _isClosing = true;
        AppLogger.Log("yazi_unexpected_exit");
        MessageBox.Show(
            this,
            "Yazi stopped unexpectedly. See the application log for details.",
            "Yazi Desktop Host",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        DisposeSession();
        Close();
    }

    private void DisposeBridge()
    {
        var session = _bridgeSession;
        var server = _bridgeServer;
        _bridgeSession = null;
        _bridgeServer = null;

        if (session is null)
        {
            server?.Dispose();
            return;
        }

        session.Disconnected -= Bridge_Disconnected;
        _ = DisposeBridgeAsync(session);
    }

    private static async Task DisposeBridgeAsync(YaziBridgeSession session)
    {
        try
        {
            await session.DisposeAsync();
        }
        catch (Exception exception)
        {
            AppLogger.Log("yazi_bridge_dispose_failed", exception);
        }
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
