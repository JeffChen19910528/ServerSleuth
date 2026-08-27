using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Analysis.Migration.Consolidation;

/// <summary>
/// Phase 8C entry point — see skill.md (Phase 8C) §1-2:
/// <c>MigrationAssessment + MigrationPlan + (RiskAggregationResult, RiskAnalysisContext, [AggregateDiscoveryResult]) -&gt; ServerMigrationAssessmentReport</c>.
///
/// This is composition ONLY — it is not a second analysis engine (§2). Every status/severity/
/// action/check value in the output is copied verbatim from an already-produced Phase 7A-8B
/// artifact; this engine's own work is limited to: joining Phase 8A's per-application assessments
/// with Phase 7B's per-application risk summaries by boundary Id, filtering Phase 8B's flat
/// Action/Check lists down to what affects each boundary, grouping dependencies by their existing
/// type, and deriving <see cref="AssessmentCoverage"/> from already-produced scanner-status
/// information. It never re-runs discovery, correlation, Risk rules, <c>MigrationPolicy</c>,
/// <c>MigrationActionPlanner</c>, or <c>GraphValidator</c>, and never touches the filesystem/
/// registry/process API/network/systemd/Docker/Podman/Kubernetes.
///
/// Never mutates any input (§20) — every list below is either a filtered/grouped VIEW over the
/// same object instances already produced upstream, or a plain count.
/// </summary>
public static class ServerMigrationAssessmentReportEngine
{
    public static ServerMigrationAssessmentReport Build(
        RiskAnalysisContext context,
        RiskAggregationResult aggregation,
        MigrationAssessmentSummary assessment,
        MigrationPlan plan,
        AggregateDiscoveryResult? discovery = null)
    {
        var riskByBoundary = aggregation.Server.ApplicationSummaries.ToDictionary(s => s.ApplicationBoundaryId, StringComparer.Ordinal);

        // Index actions/checks by affected boundary once — O(Actions/Checks * AvgBoundariesEach)
        // — instead of filtering the flat lists per application, which would be
        // O(Applications * Actions) and blow the §22 performance budget at 1,000 x 10,000 scale.
        var actionsByBoundary = IndexByBoundary(plan.Actions, a => a.AffectedBoundaryIds);
        var preChecksByBoundary = IndexByBoundary(plan.PreMigrationChecks, c => c.AffectedBoundaryIds);
        var postChecksByBoundary = IndexByBoundary(plan.PostMigrationChecks, c => c.AffectedBoundaryIds);

        var applicationAssessments = assessment.Server.ApplicationAssessments
            .Select(app => new ApplicationMigrationSummary
            {
                Assessment = app,
                RiskSeverity = riskByBoundary.TryGetValue(app.ApplicationBoundaryId, out var riskSummary)
                    ? riskSummary.OverallSeverity
                    : AggregateSeverity.None,
                Actions = actionsByBoundary.TryGetValue(app.ApplicationBoundaryId, out var acts)
                    ? acts.OrderBy(a => a.ActionId, StringComparer.Ordinal).ToList()
                    : [],
                PreMigrationChecks = preChecksByBoundary.TryGetValue(app.ApplicationBoundaryId, out var pre)
                    ? pre.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList()
                    : [],
                PostMigrationChecks = postChecksByBoundary.TryGetValue(app.ApplicationBoundaryId, out var post)
                    ? post.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList()
                    : []
            })
            .OrderBy(a => a.Assessment.ApplicationBoundaryId, StringComparer.Ordinal)
            .ToList();

        var serverLevelIssues = assessment.Server.Issues
            .Where(i => i.AffectedBoundaryIds.Count == 0)
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.RuleId, StringComparer.Ordinal)
            .ThenBy(i => i.IssueId, StringComparer.Ordinal)
            .ToList();

        var sharedInfrastructure = assessment.Server.Dependencies
            .Where(d => d.AffectedBoundaryIds.Count > 1)
            .OrderBy(d => d.Type.ToString(), StringComparer.Ordinal)
            .ThenBy(d => d.DependencyId, StringComparer.Ordinal)
            .ToList();

        var dependencyGroups = assessment.Server.Dependencies
            .GroupBy(d => d.Type)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
            .Select(g => new MigrationDependencyGroup
            {
                Type = g.Key,
                Dependencies = g.OrderBy(d => d.DependencyId, StringComparer.Ordinal).ToList()
            })
            .ToList();

        var actions = plan.Actions
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => a.ActionId, StringComparer.Ordinal)
            .ToList();

        var preMigrationChecks = plan.PreMigrationChecks.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList();
        var postMigrationChecks = plan.PostMigrationChecks.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList();

        var graphValidationErrors = context.Validation.Findings
            .Where(f => f.Severity == ValidationSeverity.Error)
            .OrderBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.EntityIds.Count > 0 ? f.EntityIds[0] : string.Empty, StringComparer.Ordinal)
            .ToList();

        var coverage = DetermineCoverage(discovery);
        var coverageWarnings = BuildCoverageWarnings(discovery);

        var serverSummary = new ServerMigrationSummary
        {
            OverallMigrationStatus = assessment.Server.OverallStatus,
            OverallRiskSeverity = aggregation.Server.OverallSeverity,
            ApplicationCount = applicationAssessments.Count,
            BlockedApplicationCount = applicationAssessments.Count(a => a.Assessment.OverallStatus == MigrationStatus.Blocked),
            NeedsRemediationApplicationCount = applicationAssessments.Count(a => a.Assessment.OverallStatus == MigrationStatus.NeedsRemediation),
            ReadyWithConditionsApplicationCount = applicationAssessments.Count(a => a.Assessment.OverallStatus == MigrationStatus.ReadyWithConditions),
            ReadyApplicationCount = applicationAssessments.Count(a => a.Assessment.OverallStatus == MigrationStatus.Ready),
            BlockingIssueCount = assessment.Server.BlockingIssueCount,
            RemediationIssueCount = assessment.Server.RemediationIssueCount,
            ConditionalDependencyCount = assessment.Server.ConditionalDependencyCount,
            ActionCount = actions.Count,
            VerificationCheckCount = preMigrationChecks.Count + postMigrationChecks.Count,
            DependencyCount = assessment.Server.Dependencies.Count,
            AffectedEntityCount = assessment.Server.AffectedEntityCount,
            AffectedBoundaryCount = assessment.Server.AffectedBoundaryCount
        };

        var diagnostics = new ConsolidationDiagnostics();
        diagnostics.RecordApplicationsConsolidated(applicationAssessments.Count);
        diagnostics.RecordServerLevelIssues(serverLevelIssues.Count);
        diagnostics.RecordSharedInfrastructureDependencies(sharedInfrastructure.Count);
        diagnostics.RecordCoverageWarnings(coverageWarnings.Count);
        diagnostics.RecordGraphValidationErrors(graphValidationErrors.Count);

        return new ServerMigrationAssessmentReport
        {
            Assessment = assessment,
            Plan = plan,
            ServerSummary = serverSummary,
            ApplicationAssessments = applicationAssessments,
            ServerLevelIssues = serverLevelIssues,
            SharedInfrastructure = sharedInfrastructure,
            Dependencies = dependencyGroups,
            Actions = actions,
            PreMigrationChecks = preMigrationChecks,
            PostMigrationChecks = postMigrationChecks,
            Coverage = coverage,
            CoverageWarnings = coverageWarnings,
            GraphValidationErrors = graphValidationErrors,
            Diagnostics = diagnostics
        };
    }

    private static Dictionary<string, List<T>> IndexByBoundary<T>(IReadOnlyList<T> items, Func<T, IReadOnlyList<string>> boundaryIdsSelector)
    {
        var index = new Dictionary<string, List<T>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            foreach (var boundaryId in boundaryIdsSelector(item))
            {
                if (!index.TryGetValue(boundaryId, out var list))
                {
                    index[boundaryId] = list = [];
                }

                list.Add(item);
            }
        }

        return index;
    }

    /// <summary>See <see cref="Consolidation.AssessmentCoverage"/>'s own doc comment for the full
    /// policy this implements.</summary>
    private static AssessmentCoverage DetermineCoverage(AggregateDiscoveryResult? discovery)
    {
        if (discovery is null || discovery.ScannerResults.Count == 0)
        {
            return AssessmentCoverage.Unknown;
        }

        if (discovery.ScannerResults.Any(r => r.Status is ScannerStatus.AccessDenied or ScannerStatus.Failed))
        {
            return AssessmentCoverage.Limited;
        }

        if (discovery.ScannerResults.Any(r => r.Status == ScannerStatus.PartiallySupported))
        {
            return AssessmentCoverage.Partial;
        }

        return AssessmentCoverage.Complete;
    }

    private static IReadOnlyList<CoverageWarning> BuildCoverageWarnings(AggregateDiscoveryResult? discovery)
    {
        if (discovery is null)
        {
            return [];
        }

        var warnings = new List<CoverageWarning>();
        foreach (var result in discovery.ScannerResults)
        {
            // NotApplicable/NotInstalled describe a legitimate absence ("nothing here to
            // discover"), never an evidence gap — only Supported is likewise unremarkable.
            if (result.Status is ScannerStatus.Supported or ScannerStatus.NotApplicable or ScannerStatus.NotInstalled)
            {
                continue;
            }

            var reason = result.Errors.Count > 0
                ? string.Join("; ", result.Errors.Select(e => e.Message))
                : $"Scanner '{result.ScannerId}' reported {result.Status}.";

            warnings.Add(new CoverageWarning
            {
                ScannerId = result.ScannerId,
                ScannerStatus = result.Status,
                Reason = reason,
                AffectedPlatform = InferPlatform(result.ScannerId),
                Evidence = result.Errors.Select(e => e.Message).ToList()
            });
        }

        return warnings.OrderBy(w => w.ScannerId, StringComparer.Ordinal).ToList();
    }

    private static string InferPlatform(string scannerId)
    {
        if (scannerId.StartsWith("windows-", StringComparison.Ordinal))
        {
            return "Windows";
        }

        if (scannerId.StartsWith("linux-", StringComparison.Ordinal))
        {
            return "Linux";
        }

        return "Unknown";
    }
}
