using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly WindowsShellContextMenuService _shellContextMenu = new();
    private TerminalContainer? _terminalContainer;
    private WindowsShellDragDropService? _shellDragDrop;
    private bool _shellContextMenuPending;
    private bool _isClosing;

    private const int WmContextMenu = 0x007B;
    private const int WmRButtonUp = 0x0205;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkF10 = 0x79;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;

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
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(AttachTerminalMessageHook));
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

        DetachTerminalMessageHook();
        _shellDragDrop?.Dispose();
        _shellDragDrop = null;

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

    private void AttachTerminalMessageHook()
    {
        if (_terminalContainer is not null || _terminal is null)
        {
            return;
        }

        _terminalContainer = FindVisualChild<TerminalContainer>(_terminal);
        if (_terminalContainer is not null)
        {
            _terminalContainer.MessageHook += Terminal_MessageHook;
            _shellDragDrop = new WindowsShellDragDropService(
                () => _bridgeSession?.State,
                _terminal);
            _shellDragDrop.Attach(_terminalContainer);
            AppLogger.Log("shell_context_menu_input_hook_attached");
        }
        else
        {
            AppLogger.Log("shell_context_menu_input_hook_unavailable");
        }
    }

    private void DetachTerminalMessageHook()
    {
        if (_terminalContainer is null)
        {
            return;
        }

        _terminalContainer.MessageHook -= Terminal_MessageHook;
        _terminalContainer = null;
    }

    private IntPtr Terminal_MessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (_isClosing)
        {
            return IntPtr.Zero;
        }

        if (_shellDragDrop is not null)
        {
            _shellDragDrop.HandleMessage(hwnd, message, wParam, ref handled);
        }

        if (message is WmContextMenu or WmRButtonUp)
        {
            var screenPoint = message == WmContextMenu
                ? DecodeScreenPoint(lParam)
                : DecodeClientPoint(hwnd, lParam);
            if (TryQueueShellContextMenu(
                    YaziShellInvocation.SelectedOrHovered,
                    (int)screenPoint.X,
                    (int)screenPoint.Y))
            {
                handled = true;
            }

            return IntPtr.Zero;
        }

        if (message is WmKeyDown or WmSysKeyDown
            && wParam.ToInt32() == VkF10
            && (GetKeyState(VkShift) & 0x8000) != 0)
        {
            var invocation = (GetKeyState(VkControl) & 0x8000) != 0
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered;
            if (GetCursorPos(out var cursor)
                && TryQueueShellContextMenu(invocation, cursor.X, cursor.Y))
            {
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private bool TryQueueShellContextMenu(YaziShellInvocation invocation, int screenX, int screenY)
    {
        if (_shellContextMenuPending)
        {
            return true;
        }

        var resolution = YaziShellTargetResolver.Resolve(_bridgeSession?.State, invocation);
        if (resolution.Status != YaziShellTargetStatus.Available)
        {
            return false;
        }

        _shellContextMenuPending = true;
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                try
                {
                    ShowShellContextMenu(invocation, screenX, screenY);
                }
                finally
                {
                    _shellContextMenuPending = false;
                }
            }));
        return true;
    }

    private void ShowShellContextMenu(YaziShellInvocation invocation, int screenX, int screenY)
    {
        if (_isClosing)
        {
            return;
        }

        var resolution = YaziShellTargetResolver.Resolve(_bridgeSession?.State, invocation);
        if (resolution.Status != YaziShellTargetStatus.Available)
        {
            AppLogger.Log($"shell_context_menu_unavailable_{resolution.Reason}");
            return;
        }

        var ownerHwnd = new WindowInteropHelper(this).Handle;
        var result = _shellContextMenu.Show(ownerHwnd, resolution.Target!, screenX, screenY);
        if (result == WindowsShellContextMenuResult.Failed)
        {
            AppLogger.Log("shell_context_menu_failed");
        }
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
            "Yazi Terminal",
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
        MessageBox.Show(this, message, "Yazi Terminal", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Point DecodeScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        if (x == -1 && y == -1 && GetCursorPos(out var cursor))
        {
            return new Point(cursor.X, cursor.Y);
        }

        return new Point(x, y);
    }

    private static Point DecodeClientPoint(IntPtr hwnd, IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var point = new CursorPoint
        {
            X = unchecked((short)(value & 0xFFFF)),
            Y = unchecked((short)((value >> 16) & 0xFFFF)),
        };
        if (ClientToScreen(hwnd, ref point))
        {
            return new Point(point.X, point.Y);
        }

        return GetCursorPos(out var cursor)
            ? new Point(cursor.X, cursor.Y)
            : new Point(0, 0);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hwnd, ref CursorPoint point);

    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
