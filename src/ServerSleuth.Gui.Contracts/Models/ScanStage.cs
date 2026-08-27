namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-3 §Step6: the coarse, real pipeline stages a scan execution passes through — matches
/// <see cref="ServerSleuth.Analysis.Orchestration.PipelineStage"/> one-for-one for the
/// post-discovery portion (Analysis/RiskAnalysis/MigrationAssessment/Reporting), plus the two
/// stages that exist only outside that pipeline call (<see cref="Preparing"/>, before any
/// transport/discovery work starts; <see cref="Discovery"/>, the scanner run itself;
/// <see cref="Export"/>, writing the already-rendered report to disk) and the three terminal
/// outcomes. Deliberately does NOT include a percentage — skill.md GUI-3 §6: "If the real
/// pipeline does not expose a meaningful percentage, use Indeterminate progress rather than
/// inventing a number" — nothing in the existing pipeline reports fractional progress within a
/// stage, so this enum only ever represents "which stage is currently running," never "how far
/// through it."
/// </summary>
public enum ScanStage
{
    Preparing,
    Discovery,
    Analysis,
    RiskAnalysis,
    MigrationAssessment,
    Reporting,
    Export,
    Completed,
    Failed,
    Cancelled
}
