using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-6A: one row in the Discovery Inventory Explorer's grid. Wraps a single already-discovered
/// <see cref="DiscoveryEntity"/> — built once, eagerly, at <see cref="InventoryExplorerViewModel"/>
/// construction time, exactly like <see cref="ApplicationRowViewModel"/> wraps one
/// <c>ApplicationMigrationSummary</c> — never re-joined or recomputed on selection/filtering/sorting.
/// </summary>
public sealed class InventoryItemViewModel
{
    public InventoryItemViewModel(DiscoveryEntity entity, IReadOnlyList<string> affectedApplications)
    {
        Detail = new InventoryDetailViewModel(entity, affectedApplications);
    }

    /// <summary>The reusable detail panel for this row — built once, shown whenever this row is
    /// selected.</summary>
    public InventoryDetailViewModel Detail { get; }

    public string Id => Detail.Id;
    public string Type => Detail.Type;
    public string Name => Detail.Name;
    public string? Version => Detail.Version;
    public EntityStatus Status => Detail.Status;
    public EntityArchitecture Architecture => Detail.Architecture;
    public string? Path => Detail.Path;
    public string? Publisher => Detail.Publisher;
    public int EvidenceCount => Detail.Evidence.Count;

    /// <summary>Grid-column display only — never a second attribution decision. 0 boundaries ⇒
    /// "Unassigned" (skill.md GUI-6A §10: "do not guess"); 1 ⇒ that application's name; 2+ ⇒
    /// every affected application, comma-joined, so a shared entity is never shown as if it
    /// belonged to only the first boundary that happened to reference it (skill.md GUI-6A §10's
    /// explicit "must not re-introduce the Phase 7B single-boundary attribution bug").</summary>
    public string ApplicationDisplay => Detail.AffectedApplications.Count switch
    {
        0 => "Unassigned",
        1 => Detail.AffectedApplications[0],
        _ => string.Join(", ", Detail.AffectedApplications)
    };
}
