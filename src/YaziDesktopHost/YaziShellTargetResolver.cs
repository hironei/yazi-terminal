namespace YaziDesktopHost;

public enum YaziShellInvocation
{
    SelectedOrHovered,
    CurrentDirectory,
}

public enum YaziShellTargetStatus
{
    Available,
    Unavailable,
    Unsupported,
    Empty,
}

public sealed record YaziShellTarget(
    YaziShellInvocation Invocation,
    IReadOnlyList<string> Paths,
    ulong BridgeSequence);

public sealed record YaziShellTargetResolution(
    YaziShellTargetStatus Status,
    YaziShellTarget? Target,
    string? Reason)
{
    public static YaziShellTargetResolution Available(YaziShellTarget target) =>
        new(YaziShellTargetStatus.Available, target, null);

    public static YaziShellTargetResolution Rejected(YaziShellTargetStatus status, string reason) =>
        new(status, null, reason);
}

public static class YaziShellTargetResolver
{
    public static YaziShellTargetResolution Resolve(
        YaziBridgeState? state,
        YaziShellInvocation invocation)
    {
        if (state is null || state.Availability != YaziBridgeAvailability.Available)
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Unavailable,
                "bridge-unavailable");
        }

        IReadOnlyList<YaziBridgePath> paths = invocation switch
        {
            YaziShellInvocation.CurrentDirectory => [state.Cwd],
            YaziShellInvocation.SelectedOrHovered when state.Selected.Count > 0 => state.Selected,
            YaziShellInvocation.SelectedOrHovered when state.Hovered is not null => [state.Hovered],
            _ => [],
        };

        if (paths.Count == 0)
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Empty,
                "no-shell-target");
        }

        if (paths.Any(path => path.Kind != YaziBridgePathKind.Filesystem))
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Unsupported,
                "non-filesystem-path");
        }

        return YaziShellTargetResolution.Available(new YaziShellTarget(
            invocation,
            paths.Select(path => path.Value).ToArray(),
            state.Sequence));
    }
}
