using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>
/// Pure, side-effect-free mapping from Phase 8C's <see cref="ServerMigrationAssessmentReport"/>
/// to the JSON contract's <see cref="ServerReportDto"/> tree — see skill.md (Phase 9A) §1, §4.
/// Every method here is a 1:1 field copy/reshape; none of them recompute a status, severity,
/// action, dependency, or check. Collections are mapped with a plain <c>Select().ToList()</c>
/// over the source's already-deterministic ordering — never grouped/sorted/deduplicated again
/// here, and never iterated via a <c>Dictionary</c>/<c>HashSet</c> (skill.md §8).
/// </summary>
internal static class ReportDtoMapper
{
    public static ServerReportDto ToDto(ServerMigrationAssessmentReport report) => new()
    {
        Server = ToDto(report.ServerSummary),
        Coverage = report.Coverage.ToString(),
        CoverageWarnings = report.CoverageWarnings.Select(ToDto).ToList(),
        Applications = report.ApplicationAssessments.Select(ToDto).ToList(),
        ServerLevelIssues = report.ServerLevelIssues.Select(ToDto).ToList(),
        SharedInfrastructure = report.SharedInfrastructure.Select(ToDto).ToList(),
        Dependencies = report.Dependencies.Select(ToDto).ToList(),
        Actions = report.Actions.Select(ToDto).ToList(),
        PreMigrationChecks = report.PreMigrationChecks.Select(ToDto).ToList(),
        PostMigrationChecks = report.PostMigrationChecks.Select(ToDto).ToList(),
        GraphValidationErrors = report.GraphValidationErrors.Select(ToDto).ToList(),
        Diagnostics = ToDto(report.Diagnostics)
    };

    private static ServerSummaryDto ToDto(ServerMigrationSummary s) => new()
    {
        OverallMigrationStatus = s.OverallMigrationStatus.ToString(),
        OverallRiskSeverity = s.OverallRiskSeverity.ToString(),
        ApplicationCount = s.ApplicationCount,
        BlockedApplicationCount = s.BlockedApplicationCount,
        NeedsRemediationApplicationCount = s.NeedsRemediationApplicationCount,
        ReadyWithConditionsApplicationCount = s.ReadyWithConditionsApplicationCount,
        ReadyApplicationCount = s.ReadyApplicationCount,
        BlockingIssueCount = s.BlockingIssueCount,
        RemediationIssueCount = s.RemediationIssueCount,
        ConditionalDependencyCount = s.ConditionalDependencyCount,
        ActionCount = s.ActionCount,
        VerificationCheckCount = s.VerificationCheckCount,
        DependencyCount = s.DependencyCount,
        AffectedEntityCount = s.AffectedEntityCount,
        AffectedBoundaryCount = s.AffectedBoundaryCount
    };

    private static ApplicationDto ToDto(ApplicationMigrationSummary a) => new()
    {
        BoundaryId = a.Assessment.ApplicationBoundaryId,
        ApplicationName = a.Assessment.ApplicationBoundaryName,
        MigrationStatus = a.Assessment.OverallStatus.ToString(),
        RiskSeverity = a.RiskSeverity.ToString(),
        AffectedEntityCount = a.Assessment.AffectedEntityCount,
        AffectedBoundaryCount = a.Assessment.AffectedBoundaryCount,
        Issues = a.Assessment.Issues.Select(ToDto).ToList(),
        Dependencies = a.Assessment.Dependencies.Select(ToDto).ToList(),
        Actions = a.Actions.Select(ToDto).ToList(),
        PreMigrationChecks = a.PreMigrationChecks.Select(ToDto).ToList(),
        PostMigrationChecks = a.PostMigrationChecks.Select(ToDto).ToList()
    };

    private static IssueDto ToDto(MigrationIssue i) => new()
    {
        IssueId = i.IssueId,
        Title = i.Title,
        Description = i.Description,
        Severity = i.Severity.ToString(),
        MigrationStatusImpact = i.MigrationStatusImpact.ToString(),
        RuleId = i.RuleId,
        SourceRiskFindingId = i.SourceRiskFindingId,
        AffectedBoundaryIds = i.AffectedBoundaryIds.ToList(),
        AffectedEntityIds = i.AffectedEntityIds.ToList(),
        Evidence = i.Evidence.Select(ToDto).ToList(),
        Confidence = ToDto(i.Confidence),
        RequiredAction = i.RequiredAction,
        PolicyDecisionReason = i.PolicyDecisionReason
    };

    private static DependencyDto ToDto(MigrationDependency d) => new()
    {
        DependencyId = d.DependencyId,
        Type = d.Type.ToString(),
        Target = d.Target,
        AffectedBoundaryIds = d.AffectedBoundaryIds.ToList(),
        Confidence = ToDto(d.Confidence),
        Evidence = d.Evidence.Select(ToDto).ToList(),
        VerificationPhase = d.VerificationPhase.ToString(),
        VerificationRequirement = d.VerificationRequirement,
        RelatedRiskFindingId = d.RelatedRiskFindingId
    };

    private static DependencyGroupDto ToDto(MigrationDependencyGroup g) => new()
    {
        Type = g.Type.ToString(),
        Dependencies = g.Dependencies.Select(ToDto).ToList()
    };

    private static ActionDto ToDto(MigrationAction a) => new()
    {
        ActionId = a.ActionId,
        ActionType = a.ActionType.ToString(),
        Title = a.Title,
        Description = a.Description,
        Priority = a.Priority.ToString(),
        Phase = a.Phase.ToString(),
        AffectedBoundaryIds = a.AffectedBoundaryIds.ToList(),
        AffectedEntityIds = a.AffectedEntityIds.ToList(),
        RelatedIssueIds = a.RelatedIssueIds.ToList(),
        RelatedDependencyIds = a.RelatedDependencyIds.ToList(),
        Evidence = a.Evidence.Select(ToDto).ToList(),
        Rationale = a.Rationale
    };

    private static CheckDto ToDto(MigrationVerificationCheck c) => new()
    {
        CheckId = c.CheckId,
        Title = c.Title,
        Description = c.Description,
        Phase = c.Phase.ToString(),
        CheckType = c.CheckType.ToString(),
        AffectedBoundaryIds = c.AffectedBoundaryIds.ToList(),
        RelatedActionIds = c.RelatedActionIds.ToList(),
        RelatedDependencyIds = c.RelatedDependencyIds.ToList(),
        Evidence = c.Evidence.Select(ToDto).ToList(),
        Rationale = c.Rationale
    };

    private static CoverageWarningDto ToDto(CoverageWarning w) => new()
    {
        ScannerId = w.ScannerId,
        ScannerStatus = w.ScannerStatus.ToString(),
        Reason = w.Reason,
        AffectedPlatform = w.AffectedPlatform,
        Evidence = w.Evidence.ToList()
    };

    private static GraphValidationFindingDto ToDto(ValidationFinding f) => new()
    {
        Category = f.Category,
        Code = f.Code,
        Severity = f.Severity.ToString(),
        Message = f.Message,
        EntityIds = f.EntityIds.ToList()
    };

    private static DiagnosticsDto ToDto(ConsolidationDiagnostics d) => new()
    {
        ApplicationsConsolidated = d.ApplicationsConsolidated,
        ServerLevelIssueCount = d.ServerLevelIssueCount,
        SharedInfrastructureDependencyCount = d.SharedInfrastructureDependencyCount,
        CoverageWarningCount = d.CoverageWarningCount,
        GraphValidationErrorCount = d.GraphValidationErrorCount
    };

    private static EvidenceDto ToDto(EvidenceRecord e) => new()
    {
        Type = e.Type.ToString(),
        Location = e.Location,
        Detail = e.Detail,
        CapturedAt = e.CapturedAt
    };

    private static ConfidenceDto ToDto(Confidence c) => new()
    {
        Value = c.Value,
        Band = c.Band.ToString()
    };
}
