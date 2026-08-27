using System.Diagnostics;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Diagnostics;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Consolidation;

/// <summary>
/// Synthetic-scale performance test — skill.md (Phase 8C) §22: 10,000 entities / 1,000 boundaries
/// / 10,000 findings / 5,000 dependencies / 10,000 actions / 20,000 verification checks / 50,000
/// relationships, all in-memory, target &lt;10s. Builds the minimal-but-valid inputs
/// <see cref="ServerMigrationAssessmentReportEngine"/> actually needs directly (mirroring every
/// earlier phase's own performance-test pattern) rather than re-running the full pipeline
/// underneath the layer under test.
/// </summary>
public class ServerMigrationAssessmentReportPerformanceTests
{
    private const int BoundaryCount = 1_000;
    private const int IssueCount = 10_000;
    private const int DependencyCount = 5_000;
    private const int ActionCount = 10_000;
    private const int CheckCountPerPhase = 10_000; // 20,000 total across Pre+Post

    [Fact]
    public void Build_TenThousandEntities_OneThousandBoundaries_LargeScale_CompletesUnderTenSeconds()
    {
        var entities = Enumerable.Range(0, 10_000)
            .Select(i => (DiscoveryEntity)new Dll { Id = $"entity:{i}", Name = $"entity{i}.dll", Type = "Dll", Source = "synthetic", Confidence = Confidence.High() })
            .ToList();

        var boundaries = Enumerable.Range(0, BoundaryCount)
            .Select(i => new ApplicationBoundary
            {
                Id = $"boundary:{i}",
                Name = $"App{i}",
                MemberEntityIds = [],
                Evidence = [],
                Confidence = Confidence.High(),
                Reason = "synthetic consolidation performance fixture"
            })
            .ToList();

        var ruleIds = new[] { "RR2-MissingBinary", "RR3-AccessDenied", "RR4-MissingRuntime", "RR9-ExternalDependency", "RR10-SharedInfrastructure" };
        var impacts = new[] { MigrationStatusImpact.Blocking, MigrationStatusImpact.RemediationRequired, MigrationStatusImpact.RemediationRequired, MigrationStatusImpact.Conditional, MigrationStatusImpact.Conditional };
        var severities = new[] { RiskSeverity.Critical, RiskSeverity.High, RiskSeverity.Medium, RiskSeverity.Low, RiskSeverity.Info };

        var issues = new List<MigrationIssue>(IssueCount);
        for (var i = 0; i < IssueCount; i++)
        {
            var ruleId = ruleIds[i % ruleIds.Length];
            var boundaryIds = Enumerable.Range(0, 5).Select(j => $"boundary:{(i + j) % BoundaryCount}").OrderBy(id => id, StringComparer.Ordinal).ToList();
            var sourceFindingId = RiskFinding.ComputeId(ruleId, $"entity:{i}");

            issues.Add(new MigrationIssue
            {
                IssueId = MigrationIssue.ComputeId(sourceFindingId),
                Title = $"Synthetic issue {i}",
                Description = "Synthetic consolidation performance fixture",
                Severity = severities[i % severities.Length],
                MigrationStatusImpact = impacts[i % impacts.Length],
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
            dependencies.Add(new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(MigrationDependencyType.Database, $"dep:{i}"),
                Type = MigrationDependencyType.Database,
                Target = $"dep-target-{i}",
                AffectedBoundaryIds = [$"boundary:{i % BoundaryCount}"],
                Confidence = Confidence.Medium(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = $"dep:{i}" }],
                VerificationPhase = MigrationVerificationPhase.PostMigration,
                VerificationRequirement = "Synthetic verification requirement",
                RelatedRiskFindingId = null
            });
        }

        var actions = new List<MigrationAction>(ActionCount);
        var priorities = Enum.GetValues<MigrationActionPriority>();
        for (var i = 0; i < ActionCount; i++)
        {
            var boundaryId = $"boundary:{i % BoundaryCount}";
            actions.Add(new MigrationAction
            {
                ActionId = $"action:synthetic:{i}",
                ActionType = MigrationActionType.PrepareConfiguration,
                Title = $"Synthetic action {i}",
                Description = "Synthetic consolidation performance fixture",
                Priority = priorities[i % priorities.Length],
                Phase = MigrationVerificationPhase.PreMigration,
                AffectedBoundaryIds = [boundaryId],
                AffectedEntityIds = [$"entity:{i}"],
                RelatedIssueIds = [],
                RelatedDependencyIds = [],
                Evidence = [],
                Rationale = "Synthetic rationale"
            });
        }

        var preChecks = new List<MigrationVerificationCheck>(CheckCountPerPhase);
        var postChecks = new List<MigrationVerificationCheck>(CheckCountPerPhase);
        for (var i = 0; i < CheckCountPerPhase; i++)
        {
            var boundaryId = $"boundary:{i % BoundaryCount}";
            preChecks.Add(new MigrationVerificationCheck
            {
                CheckId = $"check:pre:synthetic:{i}",
                Title = $"Pre check {i}",
                Description = "Synthetic",
                Phase = MigrationVerificationPhase.PreMigration,
                CheckType = MigrationActionType.VerifyConfiguration,
                AffectedBoundaryIds = [boundaryId],
                RelatedActionIds = [],
                RelatedDependencyIds = [],
                Evidence = [],
                Rationale = "Synthetic"
            });
            postChecks.Add(new MigrationVerificationCheck
            {
                CheckId = $"check:post:synthetic:{i}",
                Title = $"Post check {i}",
                Description = "Synthetic",
                Phase = MigrationVerificationPhase.PostMigration,
                CheckType = MigrationActionType.VerifyConfiguration,
                AffectedBoundaryIds = [boundaryId],
                RelatedActionIds = [],
                RelatedDependencyIds = [],
                Evidence = [],
                Rationale = "Synthetic"
            });
        }

        var server = new ServerMigrationAssessment
        {
            ApplicationAssessments = boundaries.Select(b => new ApplicationMigrationAssessment
            {
                ApplicationBoundaryId = b.Id,
                ApplicationBoundaryName = b.Name,
                OverallStatus = MigrationStatus.NeedsRemediation,
                BlockingIssueCount = 0,
                RemediationIssueCount = 1,
                ConditionalDependencyCount = 0,
                InformationalIssueCount = 0,
                UnclassifiedIssueCount = 0,
                AffectedBoundaryCount = 1,
                AffectedEntityCount = 1,
                Issues = [],
                Dependencies = [],
                Evidence = []
            }).ToList(),
            OverallStatus = MigrationStatus.Blocked,
            BlockingIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Blocking),
            RemediationIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.RemediationRequired),
            ConditionalDependencyCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Conditional),
            InformationalIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Informational),
            UnclassifiedIssueCount = 0,
            AffectedBoundaryCount = BoundaryCount,
            AffectedEntityCount = issues.Count,
            Issues = issues.OrderBy(i => i.IssueId, StringComparer.Ordinal).ToList(),
            Dependencies = dependencies.OrderBy(d => d.DependencyId, StringComparer.Ordinal).ToList(),
            Evidence = []
        };

        var assessment = new MigrationAssessmentSummary { Server = server, Diagnostics = new MigrationDiagnostics() };
        var plan = new MigrationPlan
        {
            Assessment = assessment,
            Actions = actions.OrderBy(a => a.ActionId, StringComparer.Ordinal).ToList(),
            Dependencies = server.Dependencies,
            PreMigrationChecks = preChecks.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList(),
            PostMigrationChecks = postChecks.OrderBy(c => c.CheckId, StringComparer.Ordinal).ToList(),
            Diagnostics = new MigrationPlanDiagnostics
            {
                Actions = new MigrationActionDiagnostics(),
                Verification = new MigrationVerificationDiagnostics()
            }
        };

        var boundaryResult = new BoundaryAnalysisResult { Boundaries = boundaries, Diagnostics = new BoundaryDiagnostics() };
        var expansion = new DependencyExpansionResult
        {
            ExternalDependencies = [],
            ExpandedGraph = new DependencyGraph(),
            DerivedWorkloadDependencies = [],
            Diagnostics = new ExpansionDiagnostics()
        };
        var validation = new GraphValidationResult
        {
            Findings = [],
            Orphans = [],
            Cycles = [],
            Summary = new GraphValidationSummary
            {
                TotalNodes = 0,
                TotalEdges = 0,
                ValidEdges = 0,
                InvalidEdges = 0,
                DuplicateEdges = 0,
                MissingEvidence = 0,
                DanglingEdges = 0,
                Cycles = 0,
                Orphans = 0,
                UnresolvedDependencies = 0,
                ConfidenceIssues = 0
            }
        };

        var context = new RiskAnalysisContext(entities, expansion.ExpandedGraph, boundaryResult, expansion, validation);

        var applicationSummaries = boundaries.Select(b => new ApplicationRiskSummary
        {
            ApplicationBoundaryId = b.Id,
            ApplicationBoundaryName = b.Name,
            OverallSeverity = AggregateSeverity.Medium,
            CriticalCount = 0,
            HighCount = 0,
            MediumCount = 1,
            LowCount = 0,
            InfoCount = 0,
            TotalFindingCount = 1,
            AffectedEntityCount = 1,
            AffectedBoundaryCount = 1,
            Findings = [],
            TopRisks = [],
            CategoryCounts = new Dictionary<RiskCategory, int>(),
            SharedDependencyCount = 0,
            AggregateConfidence = Confidence.Medium()
        }).ToList();

        var aggregation = new RiskAggregationResult
        {
            Server = new ServerRiskSummary
            {
                OverallSeverity = AggregateSeverity.Critical,
                CriticalCount = issues.Count(i => i.Severity == RiskSeverity.Critical),
                HighCount = issues.Count(i => i.Severity == RiskSeverity.High),
                MediumCount = issues.Count(i => i.Severity == RiskSeverity.Medium),
                LowCount = issues.Count(i => i.Severity == RiskSeverity.Low),
                InfoCount = issues.Count(i => i.Severity == RiskSeverity.Info),
                TotalFindingCount = issues.Count,
                AffectedEntityCount = issues.Count,
                AffectedBoundaryCount = BoundaryCount,
                Findings = [],
                TopRisks = [],
                CategoryCounts = new Dictionary<RiskCategory, int>(),
                SharedDependencyCount = 0,
                AggregateConfidence = Confidence.High(),
                ApplicationSummaries = applicationSummaries,
                ServerScopedFindingCount = 0
            },
            Diagnostics = new RiskAggregationDiagnostics()
        };

        var stopwatch = Stopwatch.StartNew();
        var report = ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);
        stopwatch.Stop();

        Assert.Equal(IssueCount, assessment.Server.Issues.Count);
        Assert.Equal(ActionCount, report.Actions.Count);
        Assert.Equal(BoundaryCount, report.ApplicationAssessments.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"ServerMigrationAssessmentReportEngine.Build took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected < 10s.");
    }
}
