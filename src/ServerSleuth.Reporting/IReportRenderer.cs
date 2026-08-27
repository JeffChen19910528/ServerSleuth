using ServerSleuth.Analysis.Migration.Consolidation;

namespace ServerSleuth.Reporting;

/// <summary>
/// Renders an already-produced <see cref="ServerMigrationAssessmentReport"/> (Phase 8C) into one
/// output format — see skill.md (Phase 9A) §1, §3. A renderer NEVER performs discovery,
/// correlation, risk analysis, migration policy evaluation, status calculation, dependency
/// resolution, boundary detection, or graph validation — it only reformats a result those layers
/// already produced. Rendering errors surface as thrown exceptions (§11: "rendering errors must
/// be represented as renderer failures... do not swallow exceptions") — there is no silent
/// partial/malformed-output path.
/// </summary>
public interface IReportRenderer
{
    ReportFormat Format { get; }

    ReportRenderResult Render(ServerMigrationAssessmentReport report);
}
