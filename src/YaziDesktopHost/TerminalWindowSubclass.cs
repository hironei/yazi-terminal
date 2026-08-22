using System.ComponentModel;
using System.Runtime.InteropServices;

namespace YaziDesktopHost;

internal sealed class TerminalWindowSubclass : IDisposable
{
    private const int GwlWndProc = -4;

    private readonly WindowProcedure _windowProcedure;
    private readonly Func<IntPtr, int, IntPtr, IntPtr, bool> _messageHandler;
    private IntPtr _windowHandle;
    private IntPtr _previousWindowProcedure;
    private bool _disposed;

    private TerminalWindowSubclass(
        IntPtr windowHandle,
        Func<IntPtr, int, IntPtr, IntPtr, bool> messageHandler)
    {
        _windowHandle = windowHandle;
        _messageHandler = messageHandler;
        _windowProcedure = WindowProcedureThunk;

        _previousWindowProcedure = SetWindowLongPtr(
            windowHandle,
            GwlWndProc,
            _windowProcedure);
        if (_previousWindowProcedure == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static TerminalWindowSubclass Attach(
        IntPtr windowHandle,
        Func<IntPtr, int, IntPtr, IntPtr, bool> messageHandler)
    {
        ArgumentNullException.ThrowIfNull(messageHandler);
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("The terminal window handle must not be zero.", nameof(windowHandle));
        }

        return new TerminalWindowSubclass(windowHandle, messageHandler);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_windowHandle != IntPtr.Zero && _previousWindowProcedure != IntPtr.Zero)
        {
            SetWindowLongPtr(_windowHandle, GwlWndProc, _previousWindowProcedure);
        }

        _windowHandle = IntPtr.Zero;
        _previousWindowProcedure = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    private IntPtr WindowProcedureThunk(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam)
    {
        try
        {
            if (!_disposed && _messageHandler(windowHandle, unchecked((int)message), wParam, lParam))
            {
                return IntPtr.Zero;
            }
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_context_menu_native_message_failed", exception);
        }

        return CallWindowProc(_previousWindowProcedure, windowHandle, message, wParam, lParam);
    }

    private delegate IntPtr WindowProcedure(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(
        IntPtr windowHandle,
        int index,
        WindowProcedure newWindowProcedure);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(
        IntPtr windowHandle,
        int index,
        IntPtr newWindowProcedure);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(
        IntPtr previousWindowProcedure,
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);
}
