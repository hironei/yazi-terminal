using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace YaziDesktopHost;

public enum WindowsShellContextMenuResult
{
    Invoked,
    Canceled,
    Unsupported,
    Failed,
}

internal interface IShellContextMenuMessageHandler
{
    bool TryHandleMenuMsg2(
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        out int hResult,
        out IntPtr result);

    bool TryHandleMenuMsg(
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        out int hResult);
}

internal static class ShellContextMenuMessageForwarder
{
    internal static IntPtr Forward(
        IShellContextMenuMessageHandler contextMenu,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (contextMenu.TryHandleMenuMsg2(message, wParam, lParam, out var hResult, out var result))
        {
            handled = hResult >= 0;
            return handled ? result : IntPtr.Zero;
        }

        if (contextMenu.TryHandleMenuMsg(message, wParam, lParam, out hResult))
        {
            handled = hResult >= 0;
        }

        return IntPtr.Zero;
    }
}

public sealed class WindowsShellContextMenuService
{
    private const uint CommandFirst = 1;
    private const uint CommandLast = 0x7FFF;
    private const uint CmfExplore = 0x00000004;
    private const uint CmfCanRename = 0x00000010;
    private const uint TpmRetCmd = 0x0100;
    private const uint TpmRightButton = 0x0002;
    private const uint MiimString = 0x00000040;
    private const uint MiimSubmenu = 0x00000004;
    private const uint MiimId = 0x00000002;
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
        var stage = "start";
        try
        {
            return ShowCore(ownerHwnd, target, screenX, screenY, ref stage);
        }
        catch (Exception exception)
        {
            AppLogger.Log($"shell_context_menu_failed_{stage}", exception);
            return WindowsShellContextMenuResult.Failed;
        }
    }

    private static WindowsShellContextMenuResult ShowCore(
        IntPtr ownerHwnd,
        YaziShellTarget target,
        int screenX,
        int screenY,
        ref string stage)
    {
        stage = "validate";
        if (ownerHwnd == IntPtr.Zero || target.Paths.Count == 0)
        {
            return WindowsShellContextMenuResult.Unsupported;
        }

        var pidls = new List<IntPtr>(target.Paths.Count);
        var childPidls = new List<IntPtr>(target.Paths.Count);
        var parents = new List<IShellFolder>(target.Paths.Count);
        try
        {
            foreach (var path in target.Paths)
            {
                stage = "parse_display_name";
                var hr = SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
                ThrowIfFailed(hr, "SHParseDisplayName");
                pidls.Add(pidl);

                stage = "bind_to_parent";
                var iidShellFolder = IidShellFolder;
                hr = SHBindToParent(pidl, ref iidShellFolder, out var parent, out var childPidl);
                ThrowIfFailed(hr, "SHBindToParent");
                parents.Add(parent);
                childPidls.Add(childPidl);
            }

            var parentPath = Path.GetDirectoryName(Path.GetFullPath(target.Paths[0]));
            if (target.Paths.Any(path => !string.Equals(
                    parentPath,
                    Path.GetDirectoryName(Path.GetFullPath(path)),
                    StringComparison.OrdinalIgnoreCase)))
            {
                return WindowsShellContextMenuResult.Unsupported;
            }

            var childPidlArray = Marshal.AllocCoTaskMem(IntPtr.Size * childPidls.Count);
            IntPtr contextMenuPointer = IntPtr.Zero;
            try
            {
                for (var index = 0; index < childPidls.Count; index++)
                {
                    Marshal.WriteIntPtr(childPidlArray, index * IntPtr.Size, childPidls[index]);
                }

                stage = "get_context_menu";
                var iidContextMenu = IidContextMenu;
                var hrGetMenu = parents[0].GetUIObjectOf(
                    ownerHwnd,
                    (uint)childPidls.Count,
                    childPidlArray,
                    ref iidContextMenu,
                    IntPtr.Zero,
                    out contextMenuPointer);
                ThrowIfFailed(hrGetMenu, "IShellFolder.GetUIObjectOf");
            }
            finally
            {
                Marshal.FreeCoTaskMem(childPidlArray);
            }

            IContextMenu contextMenu;
            try
            {
                contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPointer);
            }
            finally
            {
                Marshal.Release(contextMenuPointer);
            }

            try
            {
                stage = "show_menu";
                return ShowMenu(ownerHwnd, contextMenu, screenX, screenY, ref stage);
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
        int screenY,
        ref string stage)
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
            stage = "query_context_menu";
            var queryResult = QueryContextMenu(
                contextMenu,
                menu,
                0,
                CommandFirst,
                CommandLast,
                CmfExplore | CmfCanRename);
            ThrowIfFailed(queryResult, "IContextMenu.QueryContextMenu");
            LogMenuItems(menu, 0);

            if (source is not null)
            {
                hook = (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (message is WmInitMenuPopup or WmMeasureItem or WmDrawItem or WmMenuChar)
                    {
                        return ForwardMenuMessage(contextMenu, (uint)message, wParam, lParam, ref handled);
                    }

                    return IntPtr.Zero;
                };
                source.AddHook(hook);
            }

            stage = "track_popup_menu";
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

            var commandOffset = command - CommandFirst;
            AppLogger.Log($"shell_context_menu_command_{command}_offset_{commandOffset}");
            LogMenuItems(menu, 0);
            stage = "invoke_command";
            ThrowIfFailed(InvokeCommand(contextMenu, ownerHwnd, commandOffset), "IContextMenu.InvokeCommand");
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

    private static IntPtr ForwardMenuMessage(
        IContextMenu contextMenu,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled) =>
        ShellContextMenuMessageForwarder.Forward(
            new ContextMenuMessageHandler(contextMenu),
            message,
            wParam,
            lParam,
            ref handled);

    private sealed class ContextMenuMessageHandler(IContextMenu contextMenu) : IShellContextMenuMessageHandler
    {
        public bool TryHandleMenuMsg2(
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            out int hResult,
            out IntPtr result)
        {
            if (contextMenu is IContextMenu3 contextMenu3)
            {
                hResult = contextMenu3.HandleMenuMsg2(message, wParam, lParam, out result);
                return true;
            }

            hResult = 0;
            result = IntPtr.Zero;
            return false;
        }

        public bool TryHandleMenuMsg(
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            out int hResult)
        {
            if (contextMenu is IContextMenu2 contextMenu2)
            {
                hResult = contextMenu2.HandleMenuMsg(message, wParam, lParam);
                return true;
            }

            hResult = 0;
            return false;
        }
    }

    private static int QueryContextMenu(
        IContextMenu contextMenu,
        IntPtr menu,
        uint index,
        uint commandFirst,
        uint commandLast,
        uint flags)
    {
        if (contextMenu is IContextMenu3 contextMenu3)
        {
            AppLogger.Log("shell_context_menu_interface_IContextMenu3");
            return contextMenu3.QueryContextMenu(
                menu,
                index,
                commandFirst,
                commandLast,
                flags);
        }

        if (contextMenu is IContextMenu2 contextMenu2)
        {
            AppLogger.Log("shell_context_menu_interface_IContextMenu2");
            return contextMenu2.QueryContextMenu(
                menu,
                index,
                commandFirst,
                commandLast,
                flags);
        }

        AppLogger.Log("shell_context_menu_interface_IContextMenu");
        return contextMenu.QueryContextMenu(
            menu,
            index,
            commandFirst,
            commandLast,
            flags);
    }

    private static int InvokeCommand(
        IContextMenu contextMenu,
        IntPtr ownerHwnd,
        uint commandOffset)
    {
        var info = new CMINVOKECOMMANDINFO
        {
            cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
            hwnd = ownerHwnd,
            lpVerb = (IntPtr)commandOffset,
            nShow = SwShownormal,
        };
        var infoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<CMINVOKECOMMANDINFO>());
        try
        {
            Marshal.StructureToPtr(info, infoPointer, fDeleteOld: false);
            return contextMenu switch
            {
                IContextMenu3 contextMenu3 => contextMenu3.InvokeCommand(infoPointer),
                IContextMenu2 contextMenu2 => contextMenu2.InvokeCommand(infoPointer),
                _ => contextMenu.InvokeCommand(infoPointer),
            };
        }
        finally
        {
            Marshal.DestroyStructure<CMINVOKECOMMANDINFO>(infoPointer);
            Marshal.FreeCoTaskMem(infoPointer);
        }
    }

    private static void LogMenuItems(IntPtr menu, int depth)
    {
        if (menu == IntPtr.Zero || depth > 3)
        {
            return;
        }

        var count = GetMenuItemCount(menu);
        for (var index = 0; index < count; index++)
        {
            var textBuffer = Marshal.AllocCoTaskMem(256 * sizeof(char));
            try
            {
                var item = new MENUITEMINFO
                {
                    cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                    fMask = MiimId | MiimSubmenu | MiimString,
                    dwTypeData = textBuffer,
                    cch = 256,
                };
                if (!GetMenuItemInfo(menu, (uint)index, true, ref item))
                {
                    continue;
                }

                var text = Marshal.PtrToStringUni(textBuffer) ?? string.Empty;
                AppLogger.Log($"shell_context_menu_item_depth_{depth}_id_{item.wID}_text_{text}");
                if (item.hSubMenu != IntPtr.Zero)
                {
                    LogMenuItems(item.hSubMenu, depth + 1);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(textBuffer);
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMenuItemCount(IntPtr menu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMenuItemInfo(
        IntPtr menu,
        uint item,
        [MarshalAs(UnmanagedType.Bool)] bool byPosition,
        ref MENUITEMINFO info);

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
        int GetUIObjectOf(IntPtr hwndOwner, uint count, IntPtr pidls, ref Guid riid, IntPtr reserved, out IntPtr result);
        int GetDisplayNameOf(IntPtr pidl, uint flags, out IntPtr name);
        int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out IntPtr newPidl);
    }

    [ComImport]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig]
        int InvokeCommand(IntPtr info);
        [PreserveSig]
        int GetCommandString(IntPtr command, uint flags, IntPtr reserved, IntPtr name, uint max);
    }

    [ComImport]
    [Guid("000214F4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2 : IContextMenu
    {
        [PreserveSig]
        new int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig]
        new int InvokeCommand(IntPtr info);
        [PreserveSig]
        new int GetCommandString(IntPtr command, uint flags, IntPtr reserved, IntPtr name, uint max);
        [PreserveSig]
        int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
    }

    [ComImport]
    [Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3 : IContextMenu2
    {
        [PreserveSig]
        new int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags);
        [PreserveSig]
        new int InvokeCommand(IntPtr info);
        [PreserveSig]
        new int GetCommandString(IntPtr command, uint flags, IntPtr reserved, IntPtr name, uint max);
        [PreserveSig]
        new int HandleMenuMsg(uint message, IntPtr wParam, IntPtr lParam);
        [PreserveSig]
        int HandleMenuMsg2(uint message, IntPtr wParam, IntPtr lParam, out IntPtr result);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
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
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MENUITEMINFO
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public IntPtr dwItemData;
        public IntPtr dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }
}
