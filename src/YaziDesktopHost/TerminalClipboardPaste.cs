namespace YaziDesktopHost;

internal static class TerminalClipboardPaste
{
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkV = 0x56;
    private const int VkInsert = 0x2D;

    public const string StartMarker = "\u001b[200~";
    public const string EndMarker = "\u001b[201~";

    public static bool IsPasteShortcut(
        int message,
        IntPtr wParam,
        IntPtr lParam,
        bool controlDown,
        bool shiftDown)
    {
        if (message is not (WmKeyDown or WmSysKeyDown))
        {
            return false;
        }

        var isKeyRepeat = (lParam.ToInt64() & (1L << 30)) != 0;
        var virtualKey = (int)(wParam.ToInt64() & 0xFFFFL);
        return IsPasteShortcut(
            message,
            virtualKey,
            controlDown,
            shiftDown,
            isKeyRepeat);
    }

    public static bool IsPasteShortcut(
        int message,
        int virtualKey,
        bool controlDown,
        bool shiftDown,
        bool isKeyRepeat)
    {
        if (message is not (WmKeyDown or WmSysKeyDown) || isKeyRepeat)
        {
            return false;
        }

        return virtualKey switch
        {
            VkV => controlDown && shiftDown,
            VkInsert => shiftDown && !controlDown,
            _ => false,
        };
    }

    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    public static string Frame(string text) => StartMarker + text + EndMarker;
}
