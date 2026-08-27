namespace ServerSleuth.Analysis.Correlation.Validation;

/// <summary>Graph closure summary — see skill.md (Phase 5D) §22. Purely descriptive; the
/// validator never alters the graph these counts describe.</summary>
public sealed record GraphValidationSummary
{
    public required int TotalNodes { get; init; }
    public required int TotalEdges { get; init; }
    public required int ValidEdges { get; init; }
    public required int InvalidEdges { get; init; }
    public required int DuplicateEdges { get; init; }
    public required int MissingEvidence { get; init; }
    public required int DanglingEdges { get; init; }
    public required int Cycles { get; init; }
    public required int Orphans { get; init; }
    public required int UnresolvedDependencies { get; init; }
    public required int ConfidenceIssues { get; init; }
}
