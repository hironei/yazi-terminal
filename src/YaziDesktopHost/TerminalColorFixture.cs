namespace YaziDesktopHost;

internal static class TerminalColorFixture
{
    private const string EnvironmentVariable = "YAZI_DESKTOP_HOST_VT_FIXTURE";

    public static bool IsEnabled => string.Equals(
        Environment.GetEnvironmentVariable(EnvironmentVariable),
        "1",
        StringComparison.Ordinal);

    public static string Sequence =>
        "\x1b[s\x1b[999;1H\x1b[2K"
        + "Easy VT24: "
        + "\x1b[48;2;220;30;30m\x1b[38;2;255;255;255m RED \x1b[0m "
        + "\x1b[48;2;30;180;60m\x1b[38;2;0;0;0m GREEN \x1b[0m "
        + "\x1b[48;2;30;80;220m\x1b[38;2;255;255;255m BLUE \x1b[0m"
        + "\x1b[u";
}
