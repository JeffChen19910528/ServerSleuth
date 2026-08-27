namespace ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;

public sealed record RejectedMerge
{
    public required IReadOnlyList<string> InvolvedAnchorIds { get; init; }
    public required string Reason { get; init; }
}

public sealed record AmbiguousCandidate
{
    public required IReadOnlyList<string> InvolvedBoundaryIds { get; init; }
    public required string Reason { get; init; }
}

public sealed record SharedBinaryNote
{
    public required string DllEntityId { get; init; }
    public required IReadOnlyList<string> SharingAnchorIds { get; init; }
    public required string Reason { get; init; }
}

public sealed record UnresolvedOwnership
{
    public required string EntityId { get; init; }
    public required string Reason { get; init; }
}

public sealed record EvidenceConflict
{
    public required string EntityId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// Auditable record of every workload-boundary decision — see skill.md §20. Nothing a rule
/// considered disappears silently: it becomes a confirmed boundary, a merge, or one of these
/// diagnostic entries with a specific reason.
/// </summary>
public sealed class BoundaryDiagnostics
{
    private readonly List<RejectedMerge> _rejectedMerges = [];
    private readonly List<AmbiguousCandidate> _ambiguousCandidates = [];
    private readonly List<SharedBinaryNote> _sharedBinaries = [];
    private readonly List<UnresolvedOwnership> _unresolvedOwnership = [];
    private readonly List<EvidenceConflict> _evidenceConflicts = [];

    public int WorkloadCandidatesEvaluated { get; private set; }
    public int ConfirmedBoundaries { get; private set; }
    public int MergedBoundaries { get; private set; }

    public IReadOnlyList<RejectedMerge> RejectedMerges => _rejectedMerges;
    public IReadOnlyList<AmbiguousCandidate> AmbiguousCandidates => _ambiguousCandidates;
    public IReadOnlyList<SharedBinaryNote> SharedBinaries => _sharedBinaries;
    public IReadOnlyList<UnresolvedOwnership> UnresolvedOwnership => _unresolvedOwnership;

    /// <summary>Reserved for a future rule that can produce two conflicting ownership claims
    /// about the same entity — no rule in Phase 5B produces this yet, so the list is always
    /// empty today, but the type exists so a future rule has somewhere to report it rather than
    /// silently picking one side.</summary>
    public IReadOnlyList<EvidenceConflict> EvidenceConflicts => _evidenceConflicts;

    public void RecordWorkloadCandidateEvaluated() => WorkloadCandidatesEvaluated++;
    public void RecordConfirmedBoundary() => ConfirmedBoundaries++;
    public void RecordMerge() => MergedBoundaries++;

    public void RecordRejectedMerge(IReadOnlyList<string> involvedAnchorIds, string reason) =>
        _rejectedMerges.Add(new RejectedMerge { InvolvedAnchorIds = involvedAnchorIds, Reason = reason });

    public void RecordAmbiguousCandidate(IReadOnlyList<string> involvedBoundaryIds, string reason) =>
        _ambiguousCandidates.Add(new AmbiguousCandidate { InvolvedBoundaryIds = involvedBoundaryIds, Reason = reason });

    public void RecordSharedBinary(string dllEntityId, IReadOnlyList<string> sharingAnchorIds, string reason) =>
        _sharedBinaries.Add(new SharedBinaryNote { DllEntityId = dllEntityId, SharingAnchorIds = sharingAnchorIds, Reason = reason });

    public void RecordUnresolvedOwnership(string entityId, string reason) =>
        _unresolvedOwnership.Add(new UnresolvedOwnership { EntityId = entityId, Reason = reason });
}
