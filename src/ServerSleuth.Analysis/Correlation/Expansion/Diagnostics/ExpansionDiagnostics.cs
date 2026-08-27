namespace ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;

public sealed record UnresolvedReference
{
    public required string SourceEntityId { get; init; }
    public required string Reason { get; init; }
}

public sealed record ConflictingObservation
{
    public required string ExternalDependencyId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>Auditable record of Phase 5C's decisions — see skill.md (Phase 5C) §28. Every
/// candidate considered becomes a created entity, a merge, a derived relationship, or one of
/// these diagnostic entries — nothing disappears silently.</summary>
public sealed class ExpansionDiagnostics
{
    private readonly List<UnresolvedReference> _unresolvedRuntimeReferences = [];
    private readonly List<UnresolvedReference> _unresolvedComRelationships = [];
    private readonly List<ConflictingObservation> _conflictingObservations = [];

    public int ExternalDependenciesCreated { get; private set; }
    public int ExternalDependenciesMerged { get; private set; }
    public int CertificateAssociations { get; private set; }
    public int DerivedWorkloadDependencies { get; private set; }

    public IReadOnlyList<UnresolvedReference> UnresolvedRuntimeReferences => _unresolvedRuntimeReferences;
    public IReadOnlyList<UnresolvedReference> UnresolvedComRelationships => _unresolvedComRelationships;
    public IReadOnlyList<ConflictingObservation> ConflictingObservations => _conflictingObservations;

    public void RecordExternalDependencyCreated() => ExternalDependenciesCreated++;
    public void RecordExternalDependencyMerged() => ExternalDependenciesMerged++;
    public void RecordCertificateAssociation() => CertificateAssociations++;
    public void RecordDerivedWorkloadDependency() => DerivedWorkloadDependencies++;

    public void RecordUnresolvedRuntimeReference(string sourceEntityId, string reason) =>
        _unresolvedRuntimeReferences.Add(new UnresolvedReference { SourceEntityId = sourceEntityId, Reason = reason });

    public void RecordUnresolvedComRelationship(string sourceEntityId, string reason) =>
        _unresolvedComRelationships.Add(new UnresolvedReference { SourceEntityId = sourceEntityId, Reason = reason });

    public void RecordConflictingObservation(string externalDependencyId, string reason) =>
        _conflictingObservations.Add(new ConflictingObservation { ExternalDependencyId = externalDependencyId, Reason = reason });
}
