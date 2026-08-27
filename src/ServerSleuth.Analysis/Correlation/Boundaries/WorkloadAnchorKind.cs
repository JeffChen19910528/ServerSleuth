namespace ServerSleuth.Analysis.Correlation.Boundaries;

/// <summary>The three strong, explicit identity sources a workload boundary can be anchored on
/// — see skill.md §4/§6. Never anchored on a bare name or a shared/common directory alone.</summary>
public enum WorkloadAnchorKind
{
    IisApplication,
    Service,
    ScheduledTask
}
