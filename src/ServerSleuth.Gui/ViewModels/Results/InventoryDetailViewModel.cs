using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-6A: the detail panel for one selected <see cref="DiscoveryEntity"/> — every field is a
/// direct, unmodified read of that entity (or the boundary attribution
/// <see cref="InventoryExplorerViewModel"/> already resolved once at construction), never a
/// second lookup, re-scan, or re-analysis. <see cref="Metadata"/>/<see cref="Evidence"/> are
/// exposed exactly as the entity carries them — every scanner already redacts secret-shaped
/// values (see <c>ISecretRedactor</c>, invoked in <c>ServerSleuth.Windows</c>/<c>ServerSleuth.Linux</c>
/// before a <see cref="DiscoveryEntity"/> is ever constructed) so nothing here re-implements or
/// bypasses that redaction — it is presentation over already-safe data, the same guarantee the
/// existing JSON/CSV/HTML reports already rely on.
/// </summary>
public sealed class InventoryDetailViewModel
{
    public InventoryDetailViewModel(DiscoveryEntity entity, IReadOnlyList<string> affectedApplications)
    {
        Id = entity.Id;
        Type = entity.Type;
        Name = entity.Name;
        Version = entity.Version;
        Status = entity.Status;
        Architecture = entity.Architecture;
        Path = entity.Path;
        Publisher = entity.Publisher;
        Description = entity.Description;
        Source = entity.Source;
        Confidence = entity.Confidence;
        Evidence = entity.Evidence;
        Metadata = entity.Metadata;
        Tags = entity.Tags;
        AffectedApplications = affectedApplications;
    }

    public string Id { get; }
    public string Type { get; }
    public string Name { get; }
    public string? Version { get; }
    public EntityStatus Status { get; }
    public EntityArchitecture Architecture { get; }
    public string? Path { get; }
    public string? Publisher { get; }
    public string? Description { get; }
    public string Source { get; }
    public Confidence Confidence { get; }

    public IReadOnlyList<EvidenceRecord> Evidence { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
    public IReadOnlyList<string> Tags { get; }

    /// <summary>Every <c>ApplicationBoundary</c> Name whose <c>MemberEntityIds</c> already
    /// includes this entity — resolved once, in <see cref="InventoryExplorerViewModel"/>'s own
    /// boundary index, never guessed. Empty means no existing boundary evidence claims this
    /// entity, not that ownership was inferred and found absent — the UI renders that as
    /// "Unassigned" rather than treating an empty list as an error.</summary>
    public IReadOnlyList<string> AffectedApplications { get; }

    public bool HasApplicationAttribution => AffectedApplications.Count > 0;

    public bool IsSharedAcrossApplications => AffectedApplications.Count > 1;
}
