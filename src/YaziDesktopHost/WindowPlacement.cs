using System.ComponentModel;
using System.Runtime.InteropServices;

namespace YaziDesktopHost;

internal enum WindowPlacementShowState
{
    Normal,
    Maximized,
}

internal readonly record struct WindowBounds(int Left, int Top, int Right, int Bottom)
{
    public bool IsValid => Right > Left
        && Bottom > Top
        && (long)Right - Left <= int.MaxValue
        && (long)Bottom - Top <= int.MaxValue;

    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

internal sealed record MonitorWindowPlacement(
    string MonitorId,
    WindowBounds NormalBounds,
    WindowPlacementShowState ShowState);

internal sealed record WindowPlacementSettings(
    string? LastMonitorId,
    IReadOnlyList<MonitorWindowPlacement> Monitors);

internal sealed record ConnectedMonitor(string MonitorId, WindowBounds WorkArea);

internal static class WindowPlacementCatalog
{
    private const int MaximumMonitorRecords = 32;

    public static WindowPlacementSettings? Create(
        string? lastMonitorId,
        IEnumerable<MonitorWindowPlacement> placements)
    {
        var byMonitor = new Dictionary<string, MonitorWindowPlacement>(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in placements)
        {
            if (!IsValid(placement))
            {
                continue;
            }

            byMonitor[placement.MonitorId] = placement;
        }

        if (byMonitor.Count == 0)
        {
            return null;
        }

        var records = byMonitor.Values
            .Take(MaximumMonitorRecords)
            .ToArray();
        var normalizedLastMonitorId = records.FirstOrDefault(
            placement => string.Equals(
                placement.MonitorId,
                lastMonitorId,
                StringComparison.OrdinalIgnoreCase))?.MonitorId;
        return new WindowPlacementSettings(normalizedLastMonitorId, records);
    }

    public static WindowPlacementSettings Upsert(
        WindowPlacementSettings? settings,
        MonitorWindowPlacement placement)
    {
        var placements = (settings?.Monitors
            .Where(existing => !string.Equals(
                existing.MonitorId,
                placement.MonitorId,
                StringComparison.OrdinalIgnoreCase))
            .Append(placement)
            ?? [placement])
            .TakeLast(MaximumMonitorRecords);
        return Create(placement.MonitorId, placements)
            ?? new WindowPlacementSettings(placement.MonitorId, [placement]);
    }

    public static MonitorWindowPlacement? Select(
        WindowPlacementSettings? settings,
        IReadOnlyList<ConnectedMonitor> connectedMonitors)
    {
        if (settings is null || connectedMonitors.Count == 0)
        {
            return null;
        }

        var connectedIds = connectedMonitors
            .Select(monitor => monitor.MonitorId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var last = settings.Monitors.FirstOrDefault(
            placement => connectedIds.Contains(placement.MonitorId)
                && string.Equals(
                    placement.MonitorId,
                    settings.LastMonitorId,
                    StringComparison.OrdinalIgnoreCase));
        return last ?? settings.Monitors.FirstOrDefault(
            placement => connectedIds.Contains(placement.MonitorId));
    }

    public static WindowBounds Clamp(WindowBounds bounds, WindowBounds workArea)
    {
        if (!bounds.IsValid || !workArea.IsValid)
        {
            return workArea;
        }

        var width = Math.Min(bounds.Width, workArea.Width);
        var height = Math.Min(bounds.Height, workArea.Height);
        var left = Math.Clamp(bounds.Left, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(bounds.Top, workArea.Top, workArea.Bottom - height);
        return new WindowBounds(left, top, left + width, top + height);
    }

    private static bool IsValid(MonitorWindowPlacement placement)
    {
        return !string.IsNullOrWhiteSpace(placement.MonitorId)
            && placement.NormalBounds.IsValid;
    }
}

internal static class WindowPlacementNative
{
    private const int MonitorDefaultToNearest = 2;
    private const int ShowNormal = 1;
    private const int ShowMaximized = 3;
    private const uint SetWindowPosFlags = 0x54;

    public static bool TryCapture(
        IntPtr windowHandle,
        out MonitorWindowPlacement placement)
    {
        placement = null!;
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var nativePlacement = new WindowPlacement
            {
                Length = Marshal.SizeOf<WindowPlacement>(),
            };
            if (!GetWindowPlacement(windowHandle, ref nativePlacement))
            {
                if (!GetWindowRect(windowHandle, out var windowRect))
                {
                    return false;
                }

                nativePlacement.NormalPosition = windowRect;
                nativePlacement.ShowCommand = IsZoomed(windowHandle)
                    ? ShowMaximized
                    : ShowNormal;
            }

            var monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
            if (monitorHandle == IntPtr.Zero)
            {
                return false;
            }

            if (!TryGetMonitorInfo(monitorHandle, out var monitor))
            {
                return false;
            }

            var showState = nativePlacement.ShowCommand == ShowMaximized
                ? WindowPlacementShowState.Maximized
                : WindowPlacementShowState.Normal;
            placement = new MonitorWindowPlacement(
                monitor.MonitorId,
                new WindowBounds(
                    nativePlacement.NormalPosition.Left,
                    nativePlacement.NormalPosition.Top,
                    nativePlacement.NormalPosition.Right,
                    nativePlacement.NormalPosition.Bottom),
                showState);
            return WindowPlacementCatalogIsValid(placement);
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException
            or MarshalDirectiveException
            or Win32Exception)
        {
            return false;
        }
    }

    public static bool TryRestore(
        IntPtr windowHandle,
        WindowPlacementSettings? settings)
    {
        if (windowHandle == IntPtr.Zero || settings is null)
        {
            return false;
        }

        try
        {
            var connectedMonitors = GetConnectedMonitors();
            var selected = WindowPlacementCatalog.Select(settings, connectedMonitors);
            if (selected is null)
            {
                return false;
            }

            var monitor = connectedMonitors.FirstOrDefault(
                candidate => string.Equals(
                    candidate.MonitorId,
                    selected.MonitorId,
                    StringComparison.OrdinalIgnoreCase));
            if (monitor is null)
            {
                return false;
            }

            var bounds = WindowPlacementCatalog.Clamp(
                selected.NormalBounds,
                monitor.WorkArea);
            ShowWindow(windowHandle, ShowNormal);
            if (!SetWindowPos(
                    windowHandle,
                    IntPtr.Zero,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    SetWindowPosFlags))
            {
                return false;
            }

            if (selected.ShowState == WindowPlacementShowState.Maximized)
            {
                ShowWindow(windowHandle, ShowMaximized);
            }

            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException
            or MarshalDirectiveException
            or Win32Exception)
        {
            return false;
        }
    }

    private static IReadOnlyList<ConnectedMonitor> GetConnectedMonitors()
    {
        var monitors = new List<ConnectedMonitor>();
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitorHandle, _, _, _) =>
            {
                if (TryGetMonitorInfo(monitorHandle, out var monitor))
                {
                    monitors.Add(monitor);
                }

                return true;
            },
            IntPtr.Zero);
        return monitors;
    }

    private static bool TryGetMonitorInfo(
        IntPtr monitorHandle,
        out ConnectedMonitor monitor)
    {
        monitor = null!;
        var info = new MonitorInfoEx
        {
            Size = Marshal.SizeOf<MonitorInfoEx>(),
        };
        if (!GetMonitorInfo(monitorHandle, ref info)
            || string.IsNullOrWhiteSpace(info.DeviceName))
        {
            return false;
        }

        monitor = new ConnectedMonitor(
            info.DeviceName,
            new WindowBounds(
                info.WorkArea.Left,
                info.WorkArea.Top,
                info.WorkArea.Right,
                info.WorkArea.Bottom));
        return monitor.WorkArea.IsValid;
    }

    private static bool WindowPlacementCatalogIsValid(MonitorWindowPlacement placement)
    {
        return !string.IsNullOrWhiteSpace(placement.MonitorId)
            && placement.NormalBounds.IsValid;
    }

    private delegate bool MonitorEnumProc(
        IntPtr monitorHandle,
        IntPtr deviceContext,
        IntPtr monitorRect,
        IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(
        IntPtr windowHandle,
        ref WindowPlacement placement);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(
        IntPtr windowHandle,
        out NativeRect windowRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsZoomed(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(
        IntPtr windowHandle,
        int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(
        IntPtr windowHandle,
        int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(
        IntPtr monitorHandle,
        ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRect,
        MonitorEnumProc callback,
        IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public NativePoint MinPosition;
        public NativePoint MaxPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public int Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
