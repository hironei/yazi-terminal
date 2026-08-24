namespace YaziDesktopHost;

public abstract record YaziProcessExit
{
    private YaziProcessExit()
    {
    }

    public sealed record Known(int Code) : YaziProcessExit;

    public sealed record Unknown : YaziProcessExit;
}

public enum YaziProcessExitClassification
{
    Normal,
    Abnormal,
    Unknown,
}

public static class YaziProcessExitPolicy
{
    public static YaziProcessExit FromProcessMonitor(int? exitCode) => FromExitCode(exitCode);

    public static YaziProcessExit FromTerminalMarker(int? exitCode) => FromExitCode(exitCode);

    public static YaziProcessExitClassification Classify(YaziProcessExit exit) => exit switch
    {
        YaziProcessExit.Known { Code: 0 } => YaziProcessExitClassification.Normal,
        YaziProcessExit.Known => YaziProcessExitClassification.Abnormal,
        YaziProcessExit.Unknown => YaziProcessExitClassification.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(exit)),
    };

    public static bool IsNormalExit(YaziProcessExit exit) =>
        Classify(exit) == YaziProcessExitClassification.Normal;

    private static YaziProcessExit FromExitCode(int? exitCode) => exitCode is int code
        ? new YaziProcessExit.Known(code)
        : new YaziProcessExit.Unknown();
}
