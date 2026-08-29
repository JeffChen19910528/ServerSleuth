using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Diagnostics;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.TestFixtures;

/// <summary>
/// GUI-4: a hand-built, fully valid <see cref="ScanPipelineResult"/>/<see cref="ScanExecutionState"/>
/// fixture — never produced by running the real pipeline (GUI.Tests must not reference
/// Windows/Linux/Infrastructure). Every record below satisfies the exact `required` shape Phase
/// 7A-8C's own engines already produce; this factory performs none of their POLICY decisions
/// itself (it does not decide what escalates a status — it just assigns a fixed, documented
/// distribution so tests have varied, deterministic, reproducible data).
/// </summary>
public static class ScanResultFixtureFactory
{
    public sealed record Options
    {
        public int ApplicationCount { get; init; } = 3;
        public int FindingsPerApplication { get; init; } = 2;
        public int DependenciesPerApplication { get; init; } = 1;
        public int ActionsPerApplication { get; init; } = 1;
        public int ChecksPerApplication { get; init; } = 2;

        /// <summary>GUI-6 §3: server-scoped migration issues — findings that belong to the server
        /// as a whole rather than any single application boundary (an empty
        /// <c>AffectedBoundaryIds</c>), exactly as <c>ServerMigrationAssessmentReport.ServerLevelIssues</c>
        /// (Phase 8C) already distinguishes them from per-application ones.</summary>
        public int ServerLevelIssueCount { get; init; }

        /// <summary>GUI-6 §3: dependencies shared across more than one application boundary —
        /// exactly as <c>ServerMigrationAssessmentReport.SharedInfrastructure</c> (Phase 7B/8C)
        /// already distinguishes them from a single-application dependency.</summary>
        public int SharedInfrastructureCount { get; init; }

        /// <summary>GUI-6A: the raw discovered entities this fixture's <c>ScanPipelineResult.Discovery</c>
        /// carries — <c>null</c> (the default) builds an empty discovery snapshot, so every
        /// pre-GUI-6A test using this factory keeps compiling and passing unchanged.</summary>
        public IReadOnlyList<DiscoveryEntity>? DiscoveryEntities { get; init; }

        /// <summary>GUI-6A: the <c>ApplicationBoundary</c> list <c>ScanPipelineResult.Boundaries</c>
        /// carries — <c>null</c>/omitted means no boundary membership (every discovered entity
        /// shows as "Unassigned").</summary>
        public IReadOnlyList<ApplicationBoundary>? Boundaries { get; init; }

        /// <summary>GUI-6A: the <c>ExternalDependency</c> list <c>ScanPipelineResult.ExternalDependencies</c>
        /// carries.</summary>
        public IReadOnlyList<ExternalDependency>? ExternalDependencies { get; init; }
    }

    private static readonly RiskSeverity[] Severities = [RiskSeverity.Critical, RiskSeverity.High, RiskSeverity.Medium, RiskSeverity.Low, RiskSeverity.Info];
    private static readonly MigrationStatusImpact[] Impacts =
        [MigrationStatusImpact.Blocking, MigrationStatusImpact.RemediationRequired, MigrationStatusImpact.Conditional, MigrationStatusImpact.Informational];
    private static readonly MigrationDependencyType[] DependencyTypes =
        [MigrationDependencyType.Database, MigrationDependencyType.Redis, MigrationDependencyType.ExternalApi, MigrationDependencyType.FileShare, MigrationDependencyType.Certificate];

    public static ScanPipelineResult Build(Options options)
    {
        var appRiskSummaries = new List<ApplicationRiskSummary>();
        var appMigrationSummaries = new List<ApplicationMigrationSummary>();
        var allFindings = new List<RiskFinding>();
        var allDependencies = new List<MigrationDependency>();
        var allActions = new List<MigrationAction>();
        var allPreChecks = new List<MigrationVerificationCheck>();
        var allPostChecks = new List<MigrationVerificationCheck>();

        for (var appIndex = 0; appIndex < options.ApplicationCount; appIndex++)
        {
            var boundaryId = $"boundary-{appIndex:D5}";
            var boundaryName = $"Application {appIndex:D5}";

            var findings = new List<RiskFinding>();
            var issues = new List<MigrationIssue>();
            for (var f = 0; f < options.FindingsPerApplication; f++)
            {
                var severity = Severities[(appIndex + f) % Severities.Length];
                var entityId = $"entity-{appIndex:D5}-{f:D5}";
                var finding = new RiskFinding
                {
                    Id = RiskFinding.ComputeId($"RRFixture{f % 5}", entityId),
                    RuleId = $"RRFixture{f % 5}",
                    Category = RiskCategory.MissingBinary,
                    Severity = severity,
                    Confidence = Confidence.High(),
                    Title = $"Fixture finding {f} for {boundaryName}",
                    Description = "Synthetic fixture finding for GUI-4 dashboard tests.",
                    SourceEntityId = entityId,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = $@"C:\fixture\{entityId}.dll" }],
                    Recommendation = "No action required — fixture data.",
                    ApplicationBoundaryId = boundaryId
                };
                findings.Add(finding);
                allFindings.Add(finding);

                var impact = Impacts[(appIndex + f) % Impacts.Length];
                issues.Add(new MigrationIssue
                {
                    IssueId = MigrationIssue.ComputeId(finding.Id),
                    Title = finding.Title,
                    Description = finding.Description,
                    Severity = finding.Severity,
                    MigrationStatusImpact = impact,
                    RuleId = finding.RuleId,
                    SourceRiskFindingId = finding.Id,
                    AffectedBoundaryIds = [boundaryId],
                    AffectedEntityIds = [entityId],
                    Evidence = finding.Evidence,
                    Confidence = finding.Confidence,
                    RequiredAction = "Review fixture finding.",
                    PolicyDecisionReason = $"Fixture-assigned impact {impact}."
                });
            }

            var dependencies = new List<MigrationDependency>();
            for (var d = 0; d < options.DependenciesPerApplication; d++)
            {
                var type = DependencyTypes[(appIndex + d) % DependencyTypes.Length];
                var entityId = $"dependency-{appIndex:D5}-{d:D5}";
                dependencies.Add(new MigrationDependency
                {
                    DependencyId = MigrationDependency.ComputeId(type, entityId),
                    Type = type,
                    Target = $"target-{entityId}",
                    AffectedBoundaryIds = [boundaryId],
                    Confidence = Confidence.Medium(),
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = $@"C:\fixture\{entityId}.config" }],
                    VerificationPhase = MigrationVerificationPhase.PreMigration,
                    VerificationRequirement = "Confirm target is reachable post-migration."
                });
            }
            allDependencies.AddRange(dependencies);

            var actions = new List<MigrationAction>();
            for (var a = 0; a < options.ActionsPerApplication && a < issues.Count; a++)
            {
                var issue = issues[a];
                var priority = issue.Severity switch
                {
                    RiskSeverity.Critical => MigrationActionPriority.Critical,
                    RiskSeverity.High => MigrationActionPriority.High,
                    RiskSeverity.Medium => MigrationActionPriority.Medium,
                    RiskSeverity.Low => MigrationActionPriority.Low,
                    _ => MigrationActionPriority.Informational
                };
                actions.Add(new MigrationAction
                {
                    ActionId = MigrationAction.ComputeId(MigrationActionType.PrepareMissingBinary, issue.IssueId),
                    ActionType = MigrationActionType.PrepareMissingBinary,
                    Title = $"Prepare fixture binary for {boundaryName}",
                    Description = "Synthetic fixture action.",
                    Priority = priority,
                    Phase = MigrationVerificationPhase.PreMigration,
                    AffectedBoundaryIds = [boundaryId],
                    AffectedEntityIds = issue.AffectedEntityIds,
                    RelatedIssueIds = [issue.IssueId],
                    RelatedDependencyIds = [],
                    Evidence = issue.Evidence,
                    Rationale = issue.PolicyDecisionReason
                });
            }
            allActions.AddRange(actions);

            var preChecks = new List<MigrationVerificationCheck>();
            var postChecks = new List<MigrationVerificationCheck>();
            for (var c = 0; c < options.ChecksPerApplication; c++)
            {
                var sourceId = $"check-{appIndex:D5}-{c:D5}";
                var check = new MigrationVerificationCheck
                {
                    CheckId = MigrationVerificationCheck.ComputeId(MigrationVerificationPhase.PreMigration, MigrationActionType.VerifyFile, sourceId),
                    Title = $"Verify fixture file for {boundaryName} #{c}",
                    Description = "Synthetic fixture verification check.",
                    Phase = c % 2 == 0 ? MigrationVerificationPhase.PreMigration : MigrationVerificationPhase.PostMigration,
                    CheckType = MigrationActionType.VerifyFile,
                    AffectedBoundaryIds = [boundaryId],
                    RelatedActionIds = actions.Count > 0 ? [actions[0].ActionId] : [],
                    RelatedDependencyIds = dependencies.Count > 0 ? [dependencies[0].DependencyId] : [],
                    Evidence = [],
                    Rationale = "Fixture verification rationale."
                };

                if (check.Phase == MigrationVerificationPhase.PreMigration)
                {
                    preChecks.Add(check);
                }
                else
                {
                    postChecks.Add(check);
                }
            }
            allPreChecks.AddRange(preChecks);
            allPostChecks.AddRange(postChecks);

            var overallStatus = issues.Count == 0
                ? MigrationStatus.Ready
                : issues.Max(i => i.MigrationStatusImpact) switch
                {
                    MigrationStatusImpact.Blocking => MigrationStatus.Blocked,
                    MigrationStatusImpact.RemediationRequired => MigrationStatus.NeedsRemediation,
                    MigrationStatusImpact.Conditional => MigrationStatus.ReadyWithConditions,
                    _ => MigrationStatus.Ready
                };

            var assessment = new ApplicationMigrationAssessment
            {
                ApplicationBoundaryId = boundaryId,
                ApplicationBoundaryName = boundaryName,
                OverallStatus = overallStatus,
                BlockingIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Blocking),
                RemediationIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.RemediationRequired),
                ConditionalDependencyCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Conditional),
                InformationalIssueCount = issues.Count(i => i.MigrationStatusImpact == MigrationStatusImpact.Informational),
                UnclassifiedIssueCount = 0,
                AffectedBoundaryCount = 1,
                AffectedEntityCount = findings.Select(f => f.SourceEntityId).Distinct().Count(),
                Issues = issues,
                Dependencies = dependencies,
                Evidence = findings.SelectMany(f => f.Evidence).ToList()
            };

            var riskSeverity = findings.Count == 0
                ? AggregateSeverity.None
                : findings.Max(f => f.Severity).ToAggregateSeverity();

            appMigrationSummaries.Add(new ApplicationMigrationSummary
            {
                Assessment = assessment,
                RiskSeverity = riskSeverity,
                Actions = actions,
                PreMigrationChecks = preChecks,
                PostMigrationChecks = postChecks
            });

            // Mirrors Phase 7B's own real rule (ApplicationRiskAggregator): a boundary with zero
            // attributed findings never gets an ApplicationRiskSummary at all — see
            // ApplicationDetailViewModel's own doc comment for why this matters to GUI-4.
            if (findings.Count > 0)
            {
                appRiskSummaries.Add(new ApplicationRiskSummary
                {
                    ApplicationBoundaryId = boundaryId,
                    ApplicationBoundaryName = boundaryName,
                    OverallSeverity = riskSeverity,
                    CriticalCount = findings.Count(f => f.Severity == RiskSeverity.Critical),
                    HighCount = findings.Count(f => f.Severity == RiskSeverity.High),
                    MediumCount = findings.Count(f => f.Severity == RiskSeverity.Medium),
                    LowCount = findings.Count(f => f.Severity == RiskSeverity.Low),
                    InfoCount = findings.Count(f => f.Severity == RiskSeverity.Info),
                    TotalFindingCount = findings.Count,
                    AffectedEntityCount = findings.Select(f => f.SourceEntityId).Distinct().Count(),
                    AffectedBoundaryCount = 1,
                    Findings = findings,
                    TopRisks = findings.OrderByDescending(f => f.Severity).Take(5).ToList(),
                    CategoryCounts = findings.GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.Count()),
                    SharedDependencyCount = 0,
                    AggregateConfidence = new Confidence(findings.Max(f => f.Confidence.Value))
                });
            }
        }

        var serverRiskSummary = new ServerRiskSummary
        {
            OverallSeverity = allFindings.Count == 0 ? AggregateSeverity.None : allFindings.Max(f => f.Severity).ToAggregateSeverity(),
            CriticalCount = allFindings.Count(f => f.Severity == RiskSeverity.Critical),
            HighCount = allFindings.Count(f => f.Severity == RiskSeverity.High),
            MediumCount = allFindings.Count(f => f.Severity == RiskSeverity.Medium),
            LowCount = allFindings.Count(f => f.Severity == RiskSeverity.Low),
            InfoCount = allFindings.Count(f => f.Severity == RiskSeverity.Info),
            TotalFindingCount = allFindings.Count,
            AffectedEntityCount = allFindings.Select(f => f.SourceEntityId).Distinct().Count(),
            AffectedBoundaryCount = appRiskSummaries.Count(a => a.TotalFindingCount > 0),
            Findings = allFindings,
            TopRisks = allFindings.OrderByDescending(f => f.Severity).Take(10).ToList(),
            CategoryCounts = allFindings.GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.Count()),
            SharedDependencyCount = 0,
            AggregateConfidence = allFindings.Count > 0 ? new Confidence(allFindings.Max(f => f.Confidence.Value)) : new Confidence(0.0),
            ApplicationSummaries = appRiskSummaries,
            ServerScopedFindingCount = 0
        };

        var allIssues = appMigrationSummaries.SelectMany(a => a.Assessment.Issues).OrderBy(i => i.IssueId, StringComparer.Ordinal).ToList();

        var serverAssessment = new ServerMigrationAssessment
        {
            OverallStatus = appMigrationSummaries.Count == 0 ? MigrationStatus.Ready : appMigrationSummaries.Max(a => a.Assessment.OverallStatus),
            BlockingIssueCount = appMigrationSummaries.Sum(a => a.Assessment.BlockingIssueCount),
            RemediationIssueCount = appMigrationSummaries.Sum(a => a.Assessment.RemediationIssueCount),
            ConditionalDependencyCount = appMigrationSummaries.Sum(a => a.Assessment.ConditionalDependencyCount),
            InformationalIssueCount = appMigrationSummaries.Sum(a => a.Assessment.InformationalIssueCount),
            UnclassifiedIssueCount = 0,
            AffectedBoundaryCount = appMigrationSummaries.Count,
            AffectedEntityCount = appMigrationSummaries.Sum(a => a.Assessment.AffectedEntityCount),
            Issues = allIssues,
            Dependencies = allDependencies,
            Evidence = allFindings.SelectMany(f => f.Evidence).ToList(),
            ApplicationAssessments = appMigrationSummaries.Select(a => a.Assessment).ToList()
        };

        var assessmentSummary = new MigrationAssessmentSummary { Server = serverAssessment, Diagnostics = new MigrationDiagnostics() };

        var plan = new MigrationPlan
        {
            Assessment = assessmentSummary,
            Actions = allActions,
            Dependencies = allDependencies,
            PreMigrationChecks = allPreChecks,
            PostMigrationChecks = allPostChecks,
            Diagnostics = new MigrationPlanDiagnostics { Actions = new MigrationActionDiagnostics(), Verification = new MigrationVerificationDiagnostics() }
        };

        var serverSummary = new ServerMigrationSummary
        {
            OverallMigrationStatus = serverAssessment.OverallStatus,
            OverallRiskSeverity = serverRiskSummary.OverallSeverity,
            ApplicationCount = appMigrationSummaries.Count,
            BlockedApplicationCount = appMigrationSummaries.Count(a => a.Assessment.OverallStatus == MigrationStatus.Blocked),
            NeedsRemediationApplicationCount = appMigrationSummaries.Count(a => a.Assessment.OverallStatus == MigrationStatus.NeedsRemediation),
            ReadyWithConditionsApplicationCount = appMigrationSummaries.Count(a => a.Assessment.OverallStatus == MigrationStatus.ReadyWithConditions),
            ReadyApplicationCount = appMigrationSummaries.Count(a => a.Assessment.OverallStatus == MigrationStatus.Ready),
            BlockingIssueCount = serverAssessment.BlockingIssueCount,
            RemediationIssueCount = serverAssessment.RemediationIssueCount,
            ConditionalDependencyCount = serverAssessment.ConditionalDependencyCount,
            ActionCount = allActions.Count,
            VerificationCheckCount = allPreChecks.Count + allPostChecks.Count,
            DependencyCount = allDependencies.Count,
            AffectedEntityCount = serverAssessment.AffectedEntityCount,
            AffectedBoundaryCount = serverAssessment.AffectedBoundaryCount
        };

        // GUI-6 §3: server-scoped issues — no application boundary attribution at all
        // (AffectedBoundaryIds = []), so they must never be double-counted into any single
        // application's own ApplicationMigrationAssessment.Issues above.
        var serverLevelIssues = Enumerable.Range(0, options.ServerLevelIssueCount)
            .Select(i =>
            {
                var sourceRiskFindingId = $"server-level-finding-{i:D5}";
                return new MigrationIssue
                {
                    IssueId = MigrationIssue.ComputeId(sourceRiskFindingId),
                    Title = $"Server-level fixture issue {i}",
                    Description = "Synthetic server-scoped fixture issue for GUI-6 dashboard tests.",
                    Severity = RiskSeverity.Medium,
                    MigrationStatusImpact = MigrationStatusImpact.Informational,
                    RuleId = "RRFixtureServerLevel",
                    SourceRiskFindingId = sourceRiskFindingId,
                    AffectedBoundaryIds = [],
                    AffectedEntityIds = [],
                    Evidence = [],
                    Confidence = Confidence.High(),
                    RequiredAction = "Review server-level fixture issue.",
                    PolicyDecisionReason = "Fixture-assigned server-level issue."
                };
            })
            .ToList();

        // GUI-6 §3: shared infrastructure — a single dependency whose AffectedBoundaryIds spans
        // every application boundary this fixture built, exactly as Phase 7B/8C's own
        // shared-infrastructure attribution already distinguishes it from a per-application one.
        var allBoundaryIds = appMigrationSummaries.Select(a => a.Assessment.ApplicationBoundaryId).ToList();
        var sharedInfrastructure = Enumerable.Range(0, options.SharedInfrastructureCount)
            .Select(i => new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(MigrationDependencyType.Runtime, $"shared-{i:D5}"),
                Type = MigrationDependencyType.Runtime,
                Target = $"shared-runtime-{i:D5}",
                AffectedBoundaryIds = allBoundaryIds,
                Confidence = Confidence.High(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = $@"C:\fixture\shared-{i:D5}.dll" }],
                VerificationPhase = MigrationVerificationPhase.PreMigration,
                VerificationRequirement = "Confirm the shared runtime is present on the target."
            })
            .ToList();

        var dependencyGroups = allDependencies
            .GroupBy(d => d.Type)
            .OrderBy(g => g.Key)
            .Select(g => new MigrationDependencyGroup { Type = g.Key, Dependencies = g.OrderBy(d => d.DependencyId, StringComparer.Ordinal).ToList() })
            .ToList();

        var report = new ServerMigrationAssessmentReport
        {
            Assessment = assessmentSummary,
            Plan = plan,
            ServerSummary = serverSummary,
            ApplicationAssessments = appMigrationSummaries,
            ServerLevelIssues = serverLevelIssues,
            SharedInfrastructure = sharedInfrastructure,
            Dependencies = dependencyGroups,
            Actions = allActions.OrderByDescending(a => a.Priority).ThenBy(a => a.ActionId, StringComparer.Ordinal).ToList(),
            PreMigrationChecks = allPreChecks,
            PostMigrationChecks = allPostChecks,
            Coverage = AssessmentCoverage.Complete,
            CoverageWarnings = [],
            GraphValidationErrors = [],
            Diagnostics = new ConsolidationDiagnostics()
        };

        var discoveryEntities = options.DiscoveryEntities ?? [];
        var discoveryScannerResult = DiscoveryResult.Success("fixture-scanner", discoveryEntities);

        return new ScanPipelineResult
        {
            Aggregation = new RiskAggregationResult { Server = serverRiskSummary, Diagnostics = new RiskAggregationDiagnostics() },
            Report = report,
            Discovery = new AggregateDiscoveryResult
            {
                Entities = discoveryEntities,
                Errors = [],
                ScannerResults = [discoveryScannerResult],
                ScannerStatuses = new Dictionary<string, ScannerStatus> { ["fixture-scanner"] = ScannerStatus.Supported }
            },
            Boundaries = options.Boundaries ?? [],
            ExternalDependencies = options.ExternalDependencies ?? []
        };
    }

    public static ScanExecutionState BuildCompletedState(Options? options = null, ScanExecutionStatus status = ScanExecutionStatus.Completed)
    {
        var pipelineResult = Build(options ?? new Options());

        return ScanExecutionState.StartingFor(ScanTarget.Local(TargetPlatform.Windows, "Fixture Target"))
            .WithCompletion(new ScanCompletionState
        {
            Status = status,
            EntityCount = pipelineResult.Report.ServerSummary.AffectedEntityCount,
            ErrorCount = 0,
            ScannerStatuses = [new ScannerProgressInfo { ScannerId = "fixture-scanner", Status = ScannerStatus.Supported, EntityCount = 10 }],
            OutputPaths = ["report.json", "report.html"],
            PipelineResult = pipelineResult
        });
    }
}
