using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Migration.Models;

/// <summary>
/// An external or shared dependency the target environment must satisfy for migration to
/// succeed — represented separately from <see cref="MigrationIssue"/> because a dependency can
/// be entirely valid and non-risky (no RiskFinding exists for it at all) and still require
/// verification. See skill.md (Phase 8A) §10. Never call one of these a "risk" unless an actual
/// RiskFinding backs it — <see cref="RelatedRiskFindingId"/> is null whenever none does.
/// </summary>
public sealed record MigrationDependency
{
    /// <summary>Deterministic — see <see cref="ComputeId"/>.</summary>
    public required string DependencyId { get; init; }

    public required MigrationDependencyType Type { get; init; }

    /// <summary>Human-readable target — e.g. a normalized host/share/endpoint already present in
    /// discovered metadata. Never fabricated; empty when nothing more specific than the
    /// dependency's own entity Id is known.</summary>
    public required string Target { get; init; }

    /// <summary>Every ApplicationBoundary this dependency affects — ordinal-sorted; empty when
    /// the dependency has no resolvable boundary membership (still visible, never dropped).</summary>
    public required IReadOnlyList<string> AffectedBoundaryIds { get; init; }

    public required Confidence Confidence { get; init; }
    public required IReadOnlyList<EvidenceRecord> Evidence { get; init; }
    public required MigrationVerificationPhase VerificationPhase { get; init; }
    public required string VerificationRequirement { get; init; }

    /// <summary>Non-null only when an actual RiskFinding exists for this same dependency (e.g.
    /// an <c>ExternalDependencyRule</c>/<c>SharedInfrastructureRule</c> finding) — see skill.md
    /// §10: "Do not call these 'risks' unless an actual RiskFinding exists."</summary>
    public string? RelatedRiskFindingId { get; init; }

    public static string ComputeId(MigrationDependencyType type, string sourceEntityId) =>
        $"dependency:{type}:{sourceEntityId}";
}
