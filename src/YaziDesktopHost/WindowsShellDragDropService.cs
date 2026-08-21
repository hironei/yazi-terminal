using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using Microsoft.Terminal.Wpf;
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace YaziDesktopHost;

public sealed class WindowsShellDragDropService : IDisposable
{
    private const uint DropEffectNone = 0;
    private const uint DropEffectCopy = 1;
    private const uint DropEffectMove = 2;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmCaptureChanged = 0x0215;
    private const uint MkLButton = 0x0001;

    private static readonly Guid IidShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IidDropTarget = new("00000122-0000-0000-C000-000000000046");

    private readonly Func<YaziBridgeState?> _stateProvider;
    private readonly DependencyObject _dragSource;
    private ManagedDropTarget? _dropTarget;
    private IntPtr _terminalHwnd;
    private bool _registered;
    private bool _dragRunning;
    private bool _leftButtonDown;
    private Point? _dragOrigin;
    private bool _disposed;

    public WindowsShellDragDropService(
        Func<YaziBridgeState?> stateProvider,
        DependencyObject dragSource)
    {
        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _dragSource = dragSource ?? throw new ArgumentNullException(nameof(dragSource));
    }

    public bool Attach(TerminalContainer terminalContainer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_registered)
        {
            return true;
        }

        _terminalHwnd = ResolveTerminalHwnd(terminalContainer);
        if (_terminalHwnd == IntPtr.Zero)
        {
            AppLogger.Log("shell_drag_drop_hwnd_unavailable");
            return false;
        }

        _dropTarget = new ManagedDropTarget(this);
        var hResult = RegisterDragDrop(_terminalHwnd, _dropTarget);
        if (hResult < 0)
        {
            _dropTarget.Dispose();
            _dropTarget = null;
            AppLogger.Log("shell_drag_drop_register_failed", Marshal.GetExceptionForHR(hResult));
            return false;
        }

        _registered = true;
        AppLogger.Log("shell_drag_drop_attached");
        return true;
    }

    public IntPtr HandleMessage(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        ref bool handled)
    {
        if (_disposed || hwnd != _terminalHwnd)
        {
            return IntPtr.Zero;
        }

        switch (message)
        {
            case WmLButtonDown:
                _leftButtonDown = true;
                _dragOrigin = GetCursorPosition();
                break;
            case WmMouseMove:
                if (IsLeftButtonHeld(wParam))
                {
                    TryStartDrag(ref handled);
                }
                else
                {
                    ClearPendingDrag();
                }

                break;
            case WmLButtonUp:
            case WmCaptureChanged:
                ClearPendingDrag();
                break;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearPendingDrag();
        if (_registered)
        {
            var hResult = RevokeDragDrop(_terminalHwnd);
            if (hResult < 0)
            {
                AppLogger.Log("shell_drag_drop_revoke_failed", Marshal.GetExceptionForHR(hResult));
            }

            _registered = false;
        }

        _dropTarget?.Dispose();
        _dropTarget = null;
        _terminalHwnd = IntPtr.Zero;
    }

    internal static IntPtr ResolveTerminalHwnd(TerminalContainer terminalContainer)
    {
        ArgumentNullException.ThrowIfNull(terminalContainer);

        try
        {
            var property = terminalContainer.GetType().GetProperty(
                "Hwnd",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(terminalContainer) is IntPtr hwnd)
            {
                return hwnd;
            }
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_drag_drop_hwnd_lookup_failed", exception);
        }

        return IntPtr.Zero;
    }

    private void TryStartDrag(ref bool handled)
    {
        if (!_leftButtonDown || _dragRunning || _dragOrigin is not Point origin)
        {
            return;
        }

        var current = GetCursorPosition();
        if (Math.Abs(current.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var resolution = YaziShellTargetResolver.Resolve(
            _stateProvider(),
            YaziShellInvocation.SelectedOrHovered);
        if (resolution.Status != YaziShellTargetStatus.Available)
        {
            ClearPendingDrag();
            return;
        }

        _dragRunning = true;
        try
        {
            var files = new StringCollection();
            foreach (var path in resolution.Target!.Paths)
            {
                files.Add(path);
            }

            var data = new DataObject();
            data.SetFileDropList(files);
            DragDrop.DoDragDrop(
                _dragSource,
                data,
                DragDropEffects.Copy | DragDropEffects.Move);
            handled = true;
        }
        catch (COMException exception)
        {
            AppLogger.Log("shell_drag_source_failed", exception);
        }
        catch (Win32Exception exception)
        {
            AppLogger.Log("shell_drag_source_failed", exception);
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_drag_source_failed", exception);
        }
        finally
        {
            _dragRunning = false;
            ClearPendingDrag();
        }
    }

    private void ClearPendingDrag()
    {
        _leftButtonDown = false;
        _dragOrigin = null;
    }

    private static bool IsLeftButtonHeld(IntPtr wParam)
    {
        return (unchecked((uint)wParam.ToInt64()) & MkLButton) != 0;
    }

    private static Point GetCursorPosition()
    {
        return GetCursorPos(out var point)
            ? new Point(point.X, point.Y)
            : new Point(0, 0);
    }

    [ComVisible(true)]
    private sealed class ManagedDropTarget : IOleDropTarget
    {
        private readonly WindowsShellDragDropService _owner;

        public ManagedDropTarget(WindowsShellDragDropService owner)
        {
            _owner = owner;
        }

        public void DragEnter(
            ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect)
        {
            _owner.ForwardDragEnter(dataObject, keyState, point, ref effect);
        }

        public void DragOver(uint keyState, OlePoint point, ref uint effect)
        {
            _owner.ForwardDragOver(keyState, point, ref effect);
        }

        public void DragLeave()
        {
            _owner.ForwardDragLeave();
        }

        public void Drop(
            ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect)
        {
            _owner.ForwardDrop(dataObject, keyState, point, ref effect);
        }

        public void Dispose()
        {
            _owner.ForwardDragLeave();
        }
    }

    private ShellFolderDropTarget? _shellTarget;
    private string? _shellTargetPath;

    private void ForwardDragEnter(
        ComDataObject dataObject,
        uint keyState,
        OlePoint point,
        ref uint effect)
    {
        ForwardDragLeave();
        if (!TryResolveCurrentDirectory(out var path)
            || !TryCreateShellTarget(path, out var shellTarget))
        {
            effect = DropEffectNone;
            return;
        }

        _shellTarget = shellTarget!;
        _shellTargetPath = path;
        try
        {
            _shellTarget.DragEnter(dataObject, keyState, point, ref effect);
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_drag_target_failed", exception);
            ForwardDragLeave();
            effect = DropEffectNone;
        }
    }

    private void ForwardDragOver(uint keyState, OlePoint point, ref uint effect)
    {
        if (_shellTarget is null)
        {
            effect = DropEffectNone;
            return;
        }

        try
        {
            _shellTarget.DragOver(keyState, point, ref effect);
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_drag_target_failed", exception);
            ForwardDragLeave();
            effect = DropEffectNone;
        }
    }

    private void ForwardDragLeave()
    {
        ReleaseShellTarget(notifyDragLeave: true);
    }

    private void ForwardDrop(
        ComDataObject dataObject,
        uint keyState,
        OlePoint point,
        ref uint effect)
    {
        try
        {
            if (!TryResolveCurrentDirectory(out var path))
            {
                effect = DropEffectNone;
                return;
            }

            if (_shellTarget is null || !string.Equals(_shellTargetPath, path, StringComparison.OrdinalIgnoreCase))
            {
                ForwardDragLeave();
                if (!TryCreateShellTarget(path, out var shellTarget))
                {
                    effect = DropEffectNone;
                    return;
                }

                _shellTarget = shellTarget!;
                _shellTargetPath = path;
                _shellTarget.DragEnter(dataObject, keyState, point, ref effect);
            }

            _shellTarget.Drop(dataObject, keyState, point, ref effect);
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_drag_target_failed", exception);
            effect = DropEffectNone;
        }
        finally
        {
            ReleaseShellTarget(notifyDragLeave: false);
        }
    }

    private void ReleaseShellTarget(bool notifyDragLeave)
    {
        if (notifyDragLeave)
        {
            try
            {
                _shellTarget?.DragLeave();
            }
            catch (Exception exception)
            {
                AppLogger.Log("shell_drag_target_failed", exception);
            }
        }

        _shellTarget?.Dispose();
        _shellTarget = null;
        _shellTargetPath = null;
    }

    private bool TryResolveCurrentDirectory(out string path)
    {
        var resolution = YaziShellTargetResolver.Resolve(
            _stateProvider(),
            YaziShellInvocation.CurrentDirectory);
        if (resolution.Status == YaziShellTargetStatus.Available
            && resolution.Target!.Paths.Count == 1)
        {
            path = resolution.Target.Paths[0];
            return true;
        }

        path = string.Empty;
        return false;
    }

    private static bool TryCreateShellTarget(string path, out ShellFolderDropTarget? target)
    {
        try
        {
            target = ShellFolderDropTarget.Create(path);
            return true;
        }
        catch (COMException exception)
        {
            AppLogger.Log("shell_drag_target_create_failed", exception);
        }
        catch (Win32Exception exception)
        {
            AppLogger.Log("shell_drag_target_create_failed", exception);
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_drag_target_create_failed", exception);
        }

        target = null;
        return false;
    }

    private sealed class ShellFolderDropTarget : IDisposable
    {
        private IShellDropTarget? _target;
        private IShellFolder? _parent;
        private IntPtr _pidl;

        private ShellFolderDropTarget(
            IShellDropTarget target,
            IShellFolder parent,
            IntPtr pidl)
        {
            _target = target;
            _parent = parent;
            _pidl = pidl;
        }

        public static ShellFolderDropTarget Create(string path)
        {
            var hResult = SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
            ThrowIfFailed(hResult, "SHParseDisplayName");

            IShellFolder? parent = null;
            IntPtr dropTargetPointer = IntPtr.Zero;
            try
            {
                var iidShellFolder = IidShellFolder;
                hResult = SHBindToParent(
                    pidl,
                    ref iidShellFolder,
                    out parent,
                    out var childPidl);
                ThrowIfFailed(hResult, "SHBindToParent");

                var iidDropTarget = IidDropTarget;
                hResult = parent.GetUIObjectOf(
                    IntPtr.Zero,
                    1,
                    [childPidl],
                    ref iidDropTarget,
                    IntPtr.Zero,
                    out dropTargetPointer);
                ThrowIfFailed(hResult, "IShellFolder.GetUIObjectOf");

                IShellDropTarget target;
                try
                {
                    target = (IShellDropTarget)Marshal.GetObjectForIUnknown(dropTargetPointer);
                }
                finally
                {
                    Marshal.Release(dropTargetPointer);
                    dropTargetPointer = IntPtr.Zero;
                }

                return new ShellFolderDropTarget(target, parent!, pidl);
            }
            catch
            {
                if (dropTargetPointer != IntPtr.Zero)
                {
                    Marshal.Release(dropTargetPointer);
                }

                if (parent is not null)
                {
                    Marshal.ReleaseComObject(parent);
                }

                CoTaskMemFree(pidl);
                throw;
            }
        }

        public void DragEnter(
            ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect) => _target!.DragEnter(dataObject, keyState, point, ref effect);

        public void DragOver(uint keyState, OlePoint point, ref uint effect) =>
            _target!.DragOver(keyState, point, ref effect);

        public void DragLeave() => _target!.DragLeave();

        public void Drop(
            ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect) => _target!.Drop(dataObject, keyState, point, ref effect);

        public void Dispose()
        {
            if (_target is not null)
            {
                Marshal.ReleaseComObject(_target);
                _target = null;
            }

            if (_parent is not null)
            {
                Marshal.ReleaseComObject(_parent);
                _parent = null;
            }

            if (_pidl != IntPtr.Zero)
            {
                CoTaskMemFree(_pidl);
                _pidl = IntPtr.Zero;
            }
        }
    }

    private static void ThrowIfFailed(int hResult, string operation)
    {
        if (hResult < 0)
        {
            throw new COMException(operation, hResult);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int RegisterDragDrop(
        IntPtr window,
        [MarshalAs(UnmanagedType.Interface)] IOleDropTarget dropTarget);

    [DllImport("ole32.dll")]
    private static extern int RevokeDragDrop(IntPtr window);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string name,
        IntPtr bindingContext,
        out IntPtr pidl,
        uint attributes,
        out uint attributesOut);

    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellFolder parent,
        out IntPtr childPidl);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pointer);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [ComVisible(true)]
    [Guid("00000122-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleDropTarget
    {
        void DragEnter(
            [MarshalAs(UnmanagedType.Interface)] ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect);

        void DragOver(uint keyState, OlePoint point, ref uint effect);

        void DragLeave();

        void Drop(
            [MarshalAs(UnmanagedType.Interface)] ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect);
    }

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc, string name, ref uint eaten, out IntPtr pidl, ref uint attributes);
        int EnumObjects(IntPtr hwnd, uint flags, out IntPtr enumIdList);
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr result);
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr result);
        int CompareIds(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr result);
        int GetAttributesOf(uint count, IntPtr[] pidls, ref uint attributes);
        int GetUIObjectOf(IntPtr hwndOwner, uint count, IntPtr[] pidls, ref Guid riid, IntPtr reserved, out IntPtr result);
        int GetDisplayNameOf(IntPtr pidl, uint flags, out IntPtr name);
        int SetNameOf(IntPtr hwnd, IntPtr pidl, string name, uint flags, out IntPtr newPidl);
    }

    [ComImport]
    [Guid("00000122-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellDropTarget
    {
        void DragEnter(
            [MarshalAs(UnmanagedType.Interface)] ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect);

        void DragOver(uint keyState, OlePoint point, ref uint effect);

        void DragLeave();

        void Drop(
            [MarshalAs(UnmanagedType.Interface)] ComDataObject dataObject,
            uint keyState,
            OlePoint point,
            ref uint effect);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OlePoint
    {
        public int X;
        public int Y;
    }
}
