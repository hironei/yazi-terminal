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
    public static readonly TimeSpan DefaultMaxStateAge = TimeSpan.FromSeconds(1);

    public static YaziShellTargetResolution Resolve(
        YaziBridgeState? state,
        YaziShellInvocation invocation)
    {
        return Resolve(
            state,
            invocation,
            TimeProvider.System,
            DefaultMaxStateAge);
    }

    internal static YaziShellTargetResolution Resolve(
        YaziBridgeState? state,
        YaziShellInvocation invocation,
        TimeProvider timeProvider,
        TimeSpan maxStateAge)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (state is null || state.Availability != YaziBridgeAvailability.Available)
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Unavailable,
                "bridge-unavailable");
        }

        // Older bridge plugins only emit state changes. Without the negotiated
        // heartbeat capability, freshness cannot distinguish an idle session
        // from a disconnected one, so retain the legacy behavior.
        var isStale = state.SupportsHeartbeat
            && (state.LastUpdatedTimestamp != 0
                ? timeProvider.GetElapsedTime(state.LastUpdatedTimestamp) > maxStateAge
                    || timeProvider.GetElapsedTime(state.LastUpdatedTimestamp) < TimeSpan.Zero
                : timeProvider.GetUtcNow() - state.LastUpdated > maxStateAge);
        if (isStale)
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Unavailable,
                "bridge-stale");
        }

        return ResolveAvailableTarget(state, invocation);
    }

    internal static YaziShellTargetResolution Resolve(
        YaziBridgeState? state,
        YaziShellInvocation invocation,
        DateTimeOffset now,
        TimeSpan maxStateAge)
    {
        if (state is null || state.Availability != YaziBridgeAvailability.Available)
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Unavailable,
                "bridge-unavailable");
        }

        if (state.SupportsHeartbeat && now - state.LastUpdated > maxStateAge)
        {
            return YaziShellTargetResolution.Rejected(
                YaziShellTargetStatus.Unavailable,
                "bridge-stale");
        }

        return ResolveAvailableTarget(state, invocation);
    }

    private static YaziShellTargetResolution ResolveAvailableTarget(
        YaziBridgeState state,
        YaziShellInvocation invocation)
    {
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

        var normalizedPaths = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            if (!WindowsShellPathNormalizer.TryNormalize(path.Value, out var normalizedPath))
            {
                return YaziShellTargetResolution.Rejected(
                    YaziShellTargetStatus.Unsupported,
                    "invalid-filesystem-path");
            }

            normalizedPaths.Add(normalizedPath);
        }

        return YaziShellTargetResolution.Available(new YaziShellTarget(
            invocation,
            normalizedPaths,
            state.Sequence));
    }
}
