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
    private YaziShellInvocation? _rightClickInvocation;
    private TerminalWindowSubclass? _terminalWindowSubclass;
    private bool _isClosing;
    private AppThemeMode _themeMode = AppThemeMode.Dark;

    private const int WmContextMenu = 0x007B;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkF10 = 0x79;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme(_themeMode);
    }

    private void DarkThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(AppThemeMode.Dark);
    }

    private void LightThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(AppThemeMode.Light);
    }

    private void ApplyTheme(AppThemeMode mode)
    {
        _themeMode = mode;
        var colors = ThemePalette.For(mode);
        Resources["HostBackgroundBrush"] = CreateBrush(colors.HostBackground);
        Resources["HostForegroundBrush"] = CreateBrush(colors.HostForeground);
        Resources["MenuBackgroundBrush"] = CreateBrush(colors.HostBackground);
        Resources["MenuBorderBrush"] = CreateBrush(colors.MenuBorder);
        Resources["TerminalBackgroundBrush"] = CreateBrush(colors.TerminalBackground);
        DarkThemeMenuItem.IsChecked = mode == AppThemeMode.Dark;
        LightThemeMenuItem.IsChecked = mode == AppThemeMode.Light;

        ApplyTerminalTheme(colors);
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
                Theme = CreateTerminalTheme(ThemePalette.For(_themeMode)),
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

            try
            {
                _terminalWindowSubclass = TerminalWindowSubclass.Attach(
                    _terminalContainer.Handle,
                    HandleTerminalWindowMessage);
                AppLogger.Log($"shell_context_menu_native_hook_attached_{_terminalContainer.Handle.ToInt64():X}");
            }
            catch (Exception exception)
            {
                AppLogger.Log("shell_context_menu_native_hook_failed", exception);
            }

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
        _terminalWindowSubclass?.Dispose();
        _terminalWindowSubclass = null;
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

        if (_terminalWindowSubclass is not null)
        {
            return IntPtr.Zero;
        }

        if (message == WmRButtonDown)
        {
            var invocation = IsKeyDown(VkShift)
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered;
            _rightClickInvocation = CanInterceptShellContextMenu()
                ? invocation
                : null;
            if (_rightClickInvocation is not null)
            {
                handled = true;
            }

            return IntPtr.Zero;
        }

        if (message == WmRButtonUp
            || (message == WmContextMenu && !IsKeyboardContextMenu(lParam)))
        {
            var screenPoint = message == WmContextMenu
                ? DecodeScreenPoint(lParam)
                : DecodeClientPoint(hwnd, lParam);
            var invocation = _rightClickInvocation ?? (IsKeyDown(VkShift)
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered);
            var suppressNormalInput = _rightClickInvocation is not null;
            _rightClickInvocation = null;
            if (TryQueueShellContextMenu(
                    invocation,
                    (int)screenPoint.X,
                    (int)screenPoint.Y))
            {
                handled = true;
            }
            else if (suppressNormalInput)
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
            AppLogger.Log($"shell_context_menu_unavailable_{invocation}_{resolution.Reason}");
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

    private bool CanInterceptShellContextMenu()
    {
        return _bridgeSession?.State?.Availability == YaziBridgeAvailability.Available;
    }

    private bool HandleTerminalWindowMessage(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (_isClosing)
        {
            return false;
        }

        if (message is WmRButtonDown or WmRButtonUp or WmContextMenu or WmKeyDown or WmSysKeyDown)
        {
            AppLogger.Log(
                $"shell_context_menu_native_message_{MessageName(message)}_hwnd_{hwnd.ToInt64():X}"
                + $"_shift_{IsKeyDown(VkShift)}_bridge_{CanInterceptShellContextMenu()}");
        }

        if (message == WmRButtonDown)
        {
            var invocation = IsKeyDown(VkShift)
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered;
            _rightClickInvocation = CanInterceptShellContextMenu() ? invocation : null;
            return _rightClickInvocation is not null;
        }

        if (message == WmRButtonUp)
        {
            var invocation = _rightClickInvocation ?? (IsKeyDown(VkShift)
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered);
            var suppressNormalInput = _rightClickInvocation is not null;
            _rightClickInvocation = null;
            var screenPoint = DecodeClientPoint(hwnd, lParam);
            return TryQueueShellContextMenu(
                    invocation,
                    (int)screenPoint.X,
                    (int)screenPoint.Y)
                || suppressNormalInput;
        }

        if (message == WmContextMenu && !IsKeyboardContextMenu(lParam))
        {
            var screenPoint = DecodeScreenPoint(lParam);
            var invocation = IsKeyDown(VkShift)
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered;
            return TryQueueShellContextMenu(
                invocation,
                (int)screenPoint.X,
                (int)screenPoint.Y);
        }

        if (message is WmKeyDown or WmSysKeyDown
            && wParam.ToInt64() == VkF10
            && IsKeyDown(VkShift))
        {
            var invocation = IsKeyDown(VkControl)
                ? YaziShellInvocation.CurrentDirectory
                : YaziShellInvocation.SelectedOrHovered;
            return GetCursorPos(out var cursor)
                && TryQueueShellContextMenu(invocation, cursor.X, cursor.Y);
        }

        return false;
    }

    private static string MessageName(int message) => message switch
    {
        WmRButtonDown => "rbutton_down",
        WmRButtonUp => "rbutton_up",
        WmContextMenu => "context_menu",
        WmKeyDown => "key_down",
        WmSysKeyDown => "sys_key_down",
        _ => message.ToString("X4"),
    };

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
        _shellContextMenu.Show(ownerHwnd, resolution.Target!, screenX, screenY);
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

    private static TerminalTheme CreateTerminalTheme(ThemeColors colors)
    {
        return new TerminalTheme
        {
            DefaultBackground = Rgb(colors.TerminalBackground),
            DefaultForeground = Rgb(colors.TerminalForeground),
            DefaultSelectionBackground = Rgb(colors.TerminalSelectionBackground),
            CursorStyle = CursorStyle.SteadyBlock,
            ColorTable = colors.TerminalColorTable.Select(Rgb).ToArray(),
        };
    }

    private void ApplyTerminalTheme(ThemeColors colors)
    {
        var terminal = _terminal?.Terminal;
        if (terminal is null)
        {
            return;
        }

        terminal.SetTheme(
            CreateTerminalTheme(colors),
            "MS Gothic",
            14,
            ToMediaColor(colors.TerminalBackground));
    }

    private static SolidColorBrush CreateBrush(RgbColor color)
    {
        return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
    }

    private static Color ToMediaColor(RgbColor color)
    {
        return Color.FromRgb(color.Red, color.Green, color.Blue);
    }

    private static uint Rgb(RgbColor color)
    {
        return EasyTerminalControl.ColorToVal(Color.FromRgb(color.Red, color.Green, color.Blue));
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

    private static bool IsKeyboardContextMenu(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return x == -1 && y == -1;
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

    private static bool IsKeyDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

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
