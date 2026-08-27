using System.Diagnostics;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Diagnostics;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Migration.Planning;

/// <summary>
/// Synthetic-scale performance test — skill.md (Phase 8B) §26: ~10,000 MigrationIssues, ~5,000
/// dependencies, ~10,000 action/check candidates, ~50,000 affected-boundary relationships.
/// Constructs a <see cref="MigrationAssessmentSummary"/> directly (mirroring
/// <c>MigrationAssessmentPerformanceTests</c>' own pattern of building the minimal-but-valid input
/// for the layer under test, rather than running the full pipeline underneath it) — entirely
/// in-memory, no filesystem/scanner access.
/// </summary>
public class MigrationPlanPerformanceTests
{
    private const int IssueCount = 10_000;
    private const int DependencyCount = 5_000;
    private const int BoundariesPerIssue = 5; // 10,000 * 5 = 50,000 affected-boundary relationships

    [Fact]
    public void Plan_TenThousandIssues_FiveThousandDependencies_CompletesUnderTenSeconds()
    {
        var ruleIds = new[] { "RR2-MissingBinary", "RR3-AccessDenied", "RR4-MissingRuntime", "RR5-CertificateExpiry", "RR9-ExternalDependency", "RR10-SharedInfrastructure" };
        var impactsByRule = new Dictionary<string, MigrationStatusImpact>
        {
            ["RR2-MissingBinary"] = MigrationStatusImpact.Blocking,
            ["RR3-AccessDenied"] = MigrationStatusImpact.RemediationRequired,
            ["RR4-MissingRuntime"] = MigrationStatusImpact.RemediationRequired,
            ["RR5-CertificateExpiry"] = MigrationStatusImpact.RemediationRequired,
            ["RR9-ExternalDependency"] = MigrationStatusImpact.Conditional,
            ["RR10-SharedInfrastructure"] = MigrationStatusImpact.Conditional,
        };
        var severities = new[] { RiskSeverity.Critical, RiskSeverity.High, RiskSeverity.Medium, RiskSeverity.Low, RiskSeverity.Info };

        var issues = new List<MigrationIssue>(IssueCount);
        for (var i = 0; i < IssueCount; i++)
        {
            var ruleId = ruleIds[i % ruleIds.Length];
            var boundaryIds = Enumerable.Range(0, BoundariesPerIssue).Select(j => $"boundary:{i % 1000}:{j}").OrderBy(id => id, StringComparer.Ordinal).ToList();
            var sourceFindingId = RiskFinding.ComputeId(ruleId, $"entity:{i}");

            issues.Add(new MigrationIssue
            {
                IssueId = MigrationIssue.ComputeId(sourceFindingId),
                Title = $"Synthetic issue {i}",
                Description = "Synthetic migration planning performance fixture",
                Severity = severities[i % severities.Length],
                MigrationStatusImpact = impactsByRule[ruleId],
                RuleId = ruleId,
                SourceRiskFindingId = sourceFindingId,
                AffectedBoundaryIds = boundaryIds,
                AffectedEntityIds = [$"entity:{i}"],
                Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = $"entity:{i}" }],
                Confidence = Confidence.High(),
                RequiredAction = "Synthetic required action",
                PolicyDecisionReason = "Synthetic policy decision reason"
            });
        }

        Assert.True(issues.Sum(i => i.AffectedBoundaryIds.Count) >= 50_000);

        var dependencies = new List<MigrationDependency>(DependencyCount);
        for (var i = 0; i < DependencyCount; i++)
        {
            // Every other dependency traces back to one of the issues above (exercising the
            // action-dependency fold path); the rest are orphans (exercising the orphan-check path).
            var relatedFindingId = i % 2 == 0 ? issues[i % IssueCount].SourceRiskFindingId : null;

            dependencies.Add(new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(MigrationDependencyType.Database, $"dep:{i}"),
                Type = MigrationDependencyType.Database,
                Target = $"dep-target-{i}",
                AffectedBoundaryIds = [$"boundary:{i % 1000}:0"],
                Confidence = Confidence.Medium(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = $"dep:{i}" }],
                VerificationPhase = MigrationVerificationPhase.PostMigration,
                VerificationRequirement = "Synthetic verification requirement",
                RelatedRiskFindingId = relatedFindingId
            });
        }

        var server = new ServerMigrationAssessment
        {
            ApplicationAssessments = [],
            OverallStatus = MigrationStatus.Blocked,
            BlockingIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Blocking),
            RemediationIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.RemediationRequired),
            ConditionalDependencyCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Conditional),
            InformationalIssueCount = 0,
            UnclassifiedIssueCount = 0,
            AffectedBoundaryCount = 1000,
            AffectedEntityCount = issues.Count,
            Issues = issues.OrderBy(i => i.IssueId, StringComparer.Ordinal).ToList(),
            Dependencies = dependencies.OrderBy(d => d.DependencyId, StringComparer.Ordinal).ToList(),
            Evidence = []
        };

        var assessment = new MigrationAssessmentSummary { Server = server, Diagnostics = new MigrationDiagnostics() };

        var stopwatch = Stopwatch.StartNew();
        var plan = MigrationPlanEngine.Plan(assessment);
        stopwatch.Stop();

        Assert.Equal(IssueCount, plan.Actions.Count);
        Assert.True(plan.PostMigrationChecks.Count >= DependencyCount / 2);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"MigrationPlanEngine.Plan took {stopwatch.Elapsed.TotalSeconds:0.00}s for {IssueCount} issues / {DependencyCount} dependencies — expected < 10s.");
    }
}
