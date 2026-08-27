namespace ServerSleuth.Analysis.Orchestration;

/// <summary>The coarse, real stage boundaries inside <see cref="ScanPipelineRunner.Analyze"/> —
/// added in Phase GUI-3 solely so a caller that wants stage-level progress (the GUI) can observe
/// them via <see cref="ScanPipelineRunner.Analyze"/>'s optional callback parameter, without the
/// caller re-implementing or reordering a single stage itself. Deliberately coarse (matching
/// skill.md GUI-3 §6's own minimum stage list — Analysis/Risk Analysis/Migration Assessment/
/// Reporting) rather than one enum member per individual engine, since finer granularity than
/// this is not something any real caller (CLI or GUI) currently needs to observe.</summary>
public enum PipelineStage
{
    /// <summary>Correlation → Application Boundary → Dependency Expansion → Graph Validation.</summary>
    Analysis,
    RiskAnalysis,
    MigrationAssessment,
    Reporting
}
