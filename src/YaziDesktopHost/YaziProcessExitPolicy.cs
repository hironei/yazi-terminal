namespace YaziDesktopHost;

public static class YaziProcessExitPolicy
{
    public static bool IsNormalExit(int exitCode) => exitCode == 0;
}
