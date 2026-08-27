using ServerSleuth.Analysis.Migration.Diagnostics;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Migration.Assessment;

/// <summary>
/// Phase 8A entry point — transforms Phase 7A/7B output (<see cref="RiskAnalysisResult"/>,
/// <see cref="RiskAggregationResult"/>, <see cref="RiskAnalysisContext"/>) into a deterministic
/// <see cref="MigrationAssessmentSummary"/>. See skill.md (Phase 8A) §1.
///
/// Pure in-memory consumer of already-produced artifacts: never re-runs discovery, never
/// re-evaluates Risk rules, never touches the filesystem/registry/process API/network/systemd/
/// Docker/Podman/Kubernetes. Deliberately does NOT take a raw <c>GraphValidationResult</c> as
/// its own input — every Error-severity graph-integrity finding Phase 5D produced already flows
/// through as an RR12-GraphIntegrity RiskFinding (see <c>GraphIntegrityRule</c>), which this
/// engine's own policy always classifies as Blocking (skill.md §19); consuming it a second time
/// here would be "inventing a separate scanner" for information Risk Analysis already surfaced.
///
/// Never mutates any input — every returned Issue/Dependency stores the exact same RiskFinding-
/// derived data, never copies discovery entities, and confidence/evidence are carried through
/// unchanged from their source RiskFinding, never amplified (skill.md §14).
/// </summary>
public sealed class MigrationAssessmentEngine
{
    public MigrationAssessmentSummary Assess(RiskAnalysisContext context, RiskAnalysisResult riskResult, RiskAggregationResult aggregation)
    {
        var diagnostics = new MigrationDiagnostics();
        var policy = new MigrationPolicy();

        var dependencies = MigrationAssessmentCalculator.Sorted(BuildDependencies(context, riskResult.Findings, diagnostics));

        // Server-level: EVERY finding Phase 7B's ServerRiskSummary covers — application-scoped,
        // server-scoped, shared-infrastructure, and unresolved/global alike (skill.md §7).
        var serverIssues = MigrationAssessmentCalculator.Sorted(
            aggregation.Server.Findings.Select(f => MigrationPolicyEvaluator.Evaluate(f, RiskAggregator.ResolveAffectedBoundaryIds(context, f), policy, diagnostics)));

        var applicationAssessments = MigrationAssessmentCalculator.Sorted(BuildApplicationAssessments(context, aggregation, dependencies, policy, diagnostics));
        diagnostics.RecordApplicationAssessmentsCreated(applicationAssessments.Count);

        var server = new ServerMigrationAssessment
        {
            ApplicationAssessments = applicationAssessments,
            OverallStatus = MigrationAssessmentCalculator.ComputeOverallStatus(serverIssues),
            BlockingIssueCount = MigrationAssessmentCalculator.CountByImpact(serverIssues, MigrationStatusImpact.Blocking),
            RemediationIssueCount = MigrationAssessmentCalculator.CountByImpact(serverIssues, MigrationStatusImpact.RemediationRequired),
            ConditionalDependencyCount = MigrationAssessmentCalculator.CountByImpact(serverIssues, MigrationStatusImpact.Conditional),
            InformationalIssueCount = MigrationAssessmentCalculator.CountByImpact(serverIssues, MigrationStatusImpact.Informational),
            UnclassifiedIssueCount = MigrationAssessmentCalculator.CountByImpact(serverIssues, MigrationStatusImpact.Unclassified),
            AffectedBoundaryCount = applicationAssessments.Count,
            AffectedEntityCount = MigrationAssessmentCalculator.ComputeAffectedEntityCount(serverIssues),
            Issues = serverIssues,
            Dependencies = dependencies,
            Evidence = MigrationAssessmentCalculator.RollupEvidence(serverIssues, dependencies)
        };

        return new MigrationAssessmentSummary { Server = server, Diagnostics = diagnostics };
    }

    private static List<ApplicationMigrationAssessment> BuildApplicationAssessments(
        RiskAnalysisContext context,
        RiskAggregationResult aggregation,
        IReadOnlyList<MigrationDependency> allDependencies,
        MigrationPolicy policy,
        MigrationDiagnostics diagnostics)
    {
        var result = new List<ApplicationMigrationAssessment>();

        foreach (var appSummary in aggregation.Server.ApplicationSummaries)
        {
            var issues = MigrationAssessmentCalculator.Sorted(
                appSummary.Findings.Select(f => MigrationPolicyEvaluator.Evaluate(f, RiskAggregator.ResolveAffectedBoundaryIds(context, f), policy, diagnostics)));

            var dependencies = MigrationAssessmentCalculator.Sorted(
                allDependencies.Where(d => d.AffectedBoundaryIds.Contains(appSummary.ApplicationBoundaryId, StringComparer.Ordinal)));

            result.Add(new ApplicationMigrationAssessment
            {
                ApplicationBoundaryId = appSummary.ApplicationBoundaryId,
                ApplicationBoundaryName = appSummary.ApplicationBoundaryName,
                OverallStatus = MigrationAssessmentCalculator.ComputeOverallStatus(issues),
                BlockingIssueCount = MigrationAssessmentCalculator.CountByImpact(issues, MigrationStatusImpact.Blocking),
                RemediationIssueCount = MigrationAssessmentCalculator.CountByImpact(issues, MigrationStatusImpact.RemediationRequired),
                ConditionalDependencyCount = MigrationAssessmentCalculator.CountByImpact(issues, MigrationStatusImpact.Conditional),
                InformationalIssueCount = MigrationAssessmentCalculator.CountByImpact(issues, MigrationStatusImpact.Informational),
                UnclassifiedIssueCount = MigrationAssessmentCalculator.CountByImpact(issues, MigrationStatusImpact.Unclassified),
                AffectedBoundaryCount = 1,
                AffectedEntityCount = MigrationAssessmentCalculator.ComputeAffectedEntityCount(issues),
                Issues = issues,
                Dependencies = dependencies,
                Evidence = MigrationAssessmentCalculator.RollupEvidence(issues, dependencies)
            });
        }

        return result;
    }

    /// <summary>
    /// Builds <see cref="MigrationDependency"/> records from already-produced, already-
    /// normalized artifacts only — never fabricated (skill.md §10/§12): discovered
    /// <see cref="ExternalDependency"/> entities, Phase 5B's <c>BoundaryDiagnostics.SharedBinaries</c>
    /// (the same source <c>SharedInfrastructureRule</c> reads), and any Certificate/MissingRuntime
    /// RiskFinding (linked via <c>RelatedRiskFindingId</c> — a certificate/runtime requirement
    /// only becomes a tracked MigrationDependency once discovery has already surfaced a concrete
    /// signal for it, never generated speculatively for every certificate/runtime on the box).
    /// </summary>
    private static List<MigrationDependency> BuildDependencies(RiskAnalysisContext context, IReadOnlyList<RiskFinding> findings, MigrationDiagnostics diagnostics)
    {
        var dependencies = new List<MigrationDependency>();

        foreach (var dependency in context.ExternalDependencies)
        {
            var type = MapExternalDependencyType(dependency.Kind);
            var relatedFinding = findings.FirstOrDefault(f => f.Category == RiskCategory.ExternalDependency && f.SourceEntityId == dependency.Id);
            var affectedBoundaryIds = context.BoundaryIdsByEntityId.TryGetValue(dependency.Id, out var ids) ? ids : [];

            dependencies.Add(new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(type, dependency.Id),
                Type = type,
                Target = dependency.Endpoint ?? dependency.Name,
                AffectedBoundaryIds = affectedBoundaryIds,
                Confidence = dependency.Confidence,
                Evidence = dependency.Evidence,
                VerificationPhase = MigrationVerificationPhase.PostMigration,
                VerificationRequirement = $"Confirm the target environment can reach and authenticate to this {type} dependency after migration.",
                RelatedRiskFindingId = relatedFinding?.Id
            });
            diagnostics.RecordDependencyCreated();
        }

        foreach (var shared in context.BoundaryDiagnostics.SharedBinaries)
        {
            if (!context.ById.TryGetValue(shared.DllEntityId, out var dll))
            {
                continue;
            }

            var affectedBoundaryIds = shared.SharingAnchorIds
                .SelectMany(anchorId => context.BoundaryIdsByEntityId.TryGetValue(anchorId, out var ids) ? ids : [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            var relatedFinding = findings.FirstOrDefault(f => f.Category == RiskCategory.SharedInfrastructure && f.SourceEntityId == shared.DllEntityId);

            dependencies.Add(new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(MigrationDependencyType.SharedBinary, shared.DllEntityId),
                Type = MigrationDependencyType.SharedBinary,
                Target = dll.Path ?? dll.Id,
                AffectedBoundaryIds = affectedBoundaryIds,
                Confidence = dll.Confidence,
                Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = dll.Path ?? dll.Id, Detail = shared.Reason }],
                VerificationPhase = MigrationVerificationPhase.PreMigration,
                VerificationRequirement = "Confirm the shared executable exists and is reachable by every affected boundary before migration.",
                RelatedRiskFindingId = relatedFinding?.Id
            });
            diagnostics.RecordDependencyCreated();
        }

        foreach (var finding in findings.Where(f => f.Category == RiskCategory.Certificate))
        {
            var certificate = context.ById.GetValueOrDefault(finding.SourceEntityId) as Certificate;

            dependencies.Add(new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(MigrationDependencyType.Certificate, finding.SourceEntityId),
                Type = MigrationDependencyType.Certificate,
                Target = certificate?.Subject ?? finding.SourceEntityId,
                AffectedBoundaryIds = context.BoundaryIdsByEntityId.TryGetValue(finding.SourceEntityId, out var certBoundaries) ? certBoundaries : [],
                Confidence = finding.Confidence,
                Evidence = finding.Evidence,
                VerificationPhase = MigrationVerificationPhase.PreMigration,
                VerificationRequirement = "Prepare the certificate's renewal/replacement and re-binding on the target environment before or during cutover.",
                RelatedRiskFindingId = finding.Id
            });
            diagnostics.RecordDependencyCreated();
        }

        foreach (var finding in findings.Where(f => f.Category == RiskCategory.MissingRuntime))
        {
            dependencies.Add(new MigrationDependency
            {
                DependencyId = MigrationDependency.ComputeId(MigrationDependencyType.Runtime, finding.SourceEntityId),
                Type = MigrationDependencyType.Runtime,
                Target = finding.SourceEntityId,
                AffectedBoundaryIds = context.BoundaryIdsByEntityId.TryGetValue(finding.SourceEntityId, out var runtimeBoundaries) ? runtimeBoundaries : [],
                Confidence = finding.Confidence,
                Evidence = finding.Evidence,
                VerificationPhase = MigrationVerificationPhase.PreMigration,
                VerificationRequirement = "Install the explicitly-required runtime version on the target environment before migration.",
                RelatedRiskFindingId = finding.Id
            });
            diagnostics.RecordDependencyCreated();
        }

        return dependencies;
    }

    private static MigrationDependencyType MapExternalDependencyType(string? kind) => kind switch
    {
        "Database" => MigrationDependencyType.Database,
        "Redis" => MigrationDependencyType.Redis,
        "HttpApi" => MigrationDependencyType.ExternalApi,
        "Ldap" => MigrationDependencyType.Ldap,
        "FileShare" => MigrationDependencyType.FileShare,
        _ => MigrationDependencyType.Other
    };
}
