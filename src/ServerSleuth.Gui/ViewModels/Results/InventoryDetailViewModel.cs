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

        // GUI-10 §8: ScheduledTask's own typed fields (Folder/Trigger/Action/RunAsAccount/
        // Enabled/NextRun) were previously invisible in this panel — only base DiscoveryEntity
        // fields and Metadata rendered, and WindowsScheduledTaskScanner/LinuxScheduledTaskScanner
        // never put these specific fields into Metadata (they are typed properties on
        // ScheduledTask itself). This is a presentation-only projection of already-discovered
        // data — no scanner was touched, nothing is recomputed.
        if (entity is ScheduledTask task)
        {
            ScheduledTaskFolder = task.Folder;
            ScheduledTaskTrigger = task.Trigger;
            ScheduledTaskAction = task.Action;
            ScheduledTaskRunAsAccount = task.RunAsAccount;
            ScheduledTaskEnabled = task.Enabled;
            ScheduledTaskNextRun = task.NextRun;
        }
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

    // ----- GUI-10 §8: ScheduledTask-specific fields — null for every other entity type, never
    // fabricated (the constructor only sets these when the wrapped entity actually is a
    // ScheduledTask). -----
    public string? ScheduledTaskFolder { get; }
    public string? ScheduledTaskTrigger { get; }
    public string? ScheduledTaskAction { get; }
    public string? ScheduledTaskRunAsAccount { get; }
    public bool? ScheduledTaskEnabled { get; }
    public DateTimeOffset? ScheduledTaskNextRun { get; }

    public bool IsScheduledTask => ScheduledTaskEnabled.HasValue;
}
