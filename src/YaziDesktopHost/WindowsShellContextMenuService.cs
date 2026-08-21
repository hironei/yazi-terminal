using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace YaziDesktopHost;

public enum WindowsShellContextMenuResult
{
    Invoked,
    Canceled,
    Unsupported,
    Failed,
}

public sealed class WindowsShellContextMenuService
{
    private const uint CommandFirst = 1;
    private const uint CommandLast = 0x7FFF;
    private const uint CmfNormal = 0;
    private const uint TpmRetCmd = 0x0100;
    private const uint TpmRightButton = 0x0002;
    private const uint CmicMaskUnicode = 0x00004000;
    private const uint CmicMaskPtInvoke = 0x20000000;
    private const int SwShownormal = 1;
    private const int WmInitMenuPopup = 0x0117;
    private const int WmMeasureItem = 0x002C;
    private const int WmDrawItem = 0x002B;
    private const int WmMenuChar = 0x0120;

    private static readonly Guid IidShellFolder = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid IidContextMenu = new("000214E4-0000-0000-C000-000000000046");

    public WindowsShellContextMenuResult Show(
        IntPtr ownerHwnd,
        YaziShellTarget target,
        int screenX,
        int screenY)
    {
        try
        {
            return ShowCore(ownerHwnd, target, screenX, screenY);
        }
        catch (COMException exception)
        {
            AppLogger.Log("shell_context_menu_failed", exception);
            return WindowsShellContextMenuResult.Failed;
        }
        catch (Win32Exception exception)
        {
            AppLogger.Log("shell_context_menu_failed", exception);
            return WindowsShellContextMenuResult.Failed;
        }
        catch (Exception exception)
        {
            AppLogger.Log("shell_context_menu_failed", exception);
            return WindowsShellContextMenuResult.Failed;
        }
    }

    private static WindowsShellContextMenuResult ShowCore(
        IntPtr ownerHwnd,
        YaziShellTarget target,
        int screenX,
        int screenY)
    {
        if (ownerHwnd == IntPtr.Zero || target.Paths.Count == 0)
        {
            return WindowsShellContextMenuResult.Unsupported;
        }

        var pidls = new List<IntPtr>(target.Paths.Count);
        var parents = new List<IShellFolder>(target.Paths.Count);
        try
        {
            foreach (var path in target.Paths)
            {
                var hr = SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
                ThrowIfFailed(hr, "SHParseDisplayName");
                pidls.Add(pidl);

                var iidShellFolder = IidShellFolder;
                hr = SHBindToParent(pidl, ref iidShellFolder, out var parent, out _);
                ThrowIfFailed(hr, "SHBindToParent");
                parents.Add(parent);
            }

            var parentPath = Path.GetDirectoryName(Path.GetFullPath(target.Paths[0]));
            if (target.Paths.Any(path => !string.Equals(
                    parentPath,
                    Path.GetDirectoryName(Path.GetFullPath(path)),
                    StringComparison.OrdinalIgnoreCase)))
            {
                return WindowsShellContextMenuResult.Unsupported;
            }

            var childPidls = parents
                .Zip(pidls, (_, pidl) => GetLastChildPidl(pidl))
                .ToArray();
            var iidContextMenu = IidContextMenu;
            var hrGetMenu = parents[0].GetUIObjectOf(
                ownerHwnd,
                (uint)childPidls.Length,
                childPidls,
                ref iidContextMenu,
                IntPtr.Zero,
                out var contextMenuPointer);
            ThrowIfFailed(hrGetMenu, "IShellFolder.GetUIObjectOf");

            var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPointer);
            Marshal.Release(contextMenuPointer);
            try
            {
                return ShowMenu(ownerHwnd, contextMenu, screenX, screenY);
            }
            finally
            {
                Marshal.ReleaseComObject(contextMenu);
            }
        }
        finally
        {
            foreach (var parent in parents)
            {
                Marshal.ReleaseComObject(parent);
            }

            foreach (var pidl in pidls)
            {
                CoTaskMemFree(pidl);
            }
        }
    }

    private static WindowsShellContextMenuResult ShowMenu(
        IntPtr ownerHwnd,
        IContextMenu contextMenu,
        int screenX,
        int screenY)
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var source = HwndSource.FromHwnd(ownerHwnd);
        HwndSourceHook? hook = null;
        try
        {
            var queryResult = contextMenu.QueryContextMenu(
                menu,
                0,
                CommandFirst,
                CommandLast,
                CmfNormal);
            ThrowIfFailed(queryResult, "IContextMenu.QueryContextMenu");

            if (source is not null)
            {
                hook = (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (message is WmInitMenuPopup or WmMeasureItem or WmDrawItem or WmMenuChar)
                    {
                        ForwardMenuMessage(contextMenu, (uint)message, wParam, lParam, ref handled);
                    }

                    return IntPtr.Zero;
                };
                source.AddHook(hook);
            }

            var command = TrackPopupMenuEx(
                menu,
                TpmRetCmd | TpmRightButton,
                screenX,
                screenY,
                ownerHwnd,
                IntPtr.Zero);
            if (command == 0)
            {
                return WindowsShellContextMenuResult.Canceled;
            }

            var invokeInfo = new CMINVOKECOMMANDINFOEX
            {
                cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                fMask = CmicMaskUnicode | CmicMaskPtInvoke,
                hwnd = ownerHwnd,
                lpVerb = (IntPtr)(command - CommandFirst),
                lpVerbW = (IntPtr)(command - CommandFirst),
                nShow = SwShownormal,
                ptInvoke = new POINT { x = screenX, y = screenY },
            };
            ThrowIfFailed(contextMenu.InvokeCommand(ref invokeInfo), "IContextMenu.InvokeCommand");
            return WindowsShellContextMenuResult.Invoked;
        }
        finally
        {
            if (hook is not null && source is not null)
            {
                source.RemoveHook(hook);
            }

            DestroyMenu(menu);
        }
    }

    private static void ForwardMenuMessage(
        IContextMenu contextMenu,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (contextMenu is IContextMenu3 contextMenu3)
        {
            var hr = contextMenu3.HandleMenuMsg2(message, wParam, lParam, out _);
            handled = hr >= 0;
            return;
        }

        if (contextMenu is IContextMenu2 contextMenu2)
        {
            var hr = contextMenu2.HandleMenuMsg(message, wParam, lParam);
            handled = hr >= 0;
        }
    }

    private static IntPtr GetLastChildPidl(IntPtr pidl)
    {
        var current = pidl;
        while (Marshal.ReadInt16(current) != 0)
        {
            var next = current + Marshal.ReadInt16(current);
            if (Marshal.ReadInt16(next) == 0)
            {
                return current;
            }

            current = next;
        }

        throw new COMException("The Shell returned an empty PIDL.", unchecked((int)0x80004005));
    }

    private static void ThrowIfFailed(int hResult, string operation)
    {
        if (hResult < 0)
        {
            Marshal.ThrowExceptionForHR(hResult);
        }
    }

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
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr owner,
        IntPtr parameters);

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string name, ref uint eaten, out IntPtr pidl, ref uint attributes);
        int EnumObjects(IntPtr hwnd, uint flags, out IntPtr enumIdList);
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr result);
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr result);
        int CompareIds(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr result);
        int GetAttributesOf(uint count, IntPtr[] pidls, ref uint attributes);
        int GetUIObjectOf(IntPtr hwndOwner, uint count, IntPtr[] pidls, ref Guid riid, IntPtr reserved, out IntPtr result);
        int GetDisplayNameOf(IntPtr pidl, uint flags, out IntPtr name);
        int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out IntPtr newPidl);
    }

    [ComImport]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX info);
        int GetCommandString(IntPtr command, uint flags, IntPtr reserved, StringBuilder name, uint max);
    }

    [ComImport]
    [Guid("000214F4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2 : IContextMenu
    {
        int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
    }

    [ComImport]
    [Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3 : IContextMenu2
    {
        int HandleMenuMsg2(uint message, IntPtr wParam, IntPtr lParam, out IntPtr result);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;
    }
}
