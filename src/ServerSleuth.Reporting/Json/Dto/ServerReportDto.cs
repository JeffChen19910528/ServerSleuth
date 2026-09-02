using ServerSleuth.Analysis.Migration.Preparation;

namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>
/// The root JSON contract — see skill.md (Phase 9A) §5. Every field maps 1:1 to something
/// <see cref="ServerSleuth.Analysis.Migration.Consolidation.ServerMigrationAssessmentReport"/>
/// (Phase 8C) already produced; nothing here is computed, only reshaped for serialization. Field
/// order below is JSON property order (System.Text.Json serializes in declaration order), and
/// every collection is already ordinal-sorted by the source Phase 8C model — this DTO never
/// re-sorts or re-derives ordering itself.
/// </summary>
public sealed record ServerReportDto
{
    public required ServerSummaryDto Server { get; init; }
    public required string Coverage { get; init; }
    public required IReadOnlyList<CoverageWarningDto> CoverageWarnings { get; init; }
    public required IReadOnlyList<ApplicationDto> Applications { get; init; }
    public required IReadOnlyList<IssueDto> ServerLevelIssues { get; init; }
    public required IReadOnlyList<DependencyDto> SharedInfrastructure { get; init; }
    public required IReadOnlyList<DependencyGroupDto> Dependencies { get; init; }
    public required IReadOnlyList<ActionDto> Actions { get; init; }
    public required IReadOnlyList<CheckDto> PreMigrationChecks { get; init; }
    public required IReadOnlyList<CheckDto> PostMigrationChecks { get; init; }
    public required IReadOnlyList<GraphValidationFindingDto> GraphValidationErrors { get; init; }
    public required DiagnosticsDto Diagnostics { get; init; }

    // GUI-8C: server-wide inventory sections. Empty by default for backward compatibility
    // (reports built without inventory data simply omit these sections). Each list is sorted
    // Name then Id for deterministic output. Entities not attributed to any boundary have
    // ApplicationName = null.
    public IReadOnlyList<InventoryEntityDto> DllBinaries { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> Runtimes { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> Services { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> ComComponents { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> Software { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> ScheduledTasks { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> Certificates { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> Configurations { get; init; } = [];
    public IReadOnlyList<InventoryEntityDto> ExternalConnections { get; init; } = [];

    /// <summary>GUI-9B: a computed, inventory-derived "what must be prepared on the destination
    /// server" projection over the nine lists above plus <c>Applications</c> — see
    /// <see cref="MigrationPreparationSummary"/>. Defaults to
    /// <see cref="MigrationPreparationSummary.Empty"/> for the same backward-compatibility reason
    /// the nine inventory lists default to <c>[]</c>: a report built without discovery data
    /// (the plain <c>ToDto(report)</c> overload) has nothing to summarize.</summary>
    public MigrationPreparationSummary MigrationPreparation { get; init; } = MigrationPreparationSummary.Empty;
}
