namespace ServerSleuth.Analysis.Correlation.Validation;

/// <summary>
/// One structural, semantic, provenance, or evidence-quality problem found in the graph — see
/// skill.md (Phase 5D) §2-10. The validator only ever reports; it never repairs the graph.
/// </summary>
public sealed record ValidationFinding
{
    /// <summary>Broad category, e.g. "NodeIntegrity", "EdgeIntegrity", "DuplicateEdge",
    /// "MissingEvidence", "ProvenanceIntegrity", "ConfidenceConsistency", "SelfEdge",
    /// "UnresolvedDependency", "ExternalDependency", "Certificate", "Com", "Runtime".</summary>
    public required string Category { get; init; }

    /// <summary>Specific machine-readable code, e.g. "DuplicateNodeId", "DanglingSource",
    /// "InvalidRelationshipType", "MissingEvidence", "InvalidEvidenceType", "EmptyProvenance",
    /// "ConfidenceEscalation", "ConfidenceWithoutEvidence", "InvalidSelfEdge",
    /// "PotentialLegitimateSelfReference", "MissingBinary", "UnresolvedBinary",
    /// "UnresolvedCertificate", "UnresolvedComReference", "RuntimeMismatch",
    /// "ExternalDependencyIdentityConflict".</summary>
    public required string Code { get; init; }

    public required ValidationSeverity Severity { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> EntityIds { get; init; } = [];
}
