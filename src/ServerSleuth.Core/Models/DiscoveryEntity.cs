using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Core.Models;

/// <summary>
/// Common base for every discovered entity — see skill.md §5. Concrete entities add only
/// the fields relevant to them; nothing here is forced onto an entity type it doesn't apply to.
/// </summary>
public abstract class DiscoveryEntity
{
    private readonly List<EvidenceRecord> _evidence = [];
    private readonly List<string> _tags = [];
    private readonly Dictionary<string, string> _metadata = new();

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Version { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Unknown;
    public EntityArchitecture Architecture { get; init; } = EntityArchitecture.Unknown;
    public string? Path { get; init; }
    public string? Publisher { get; set; }
    public string? Description { get; init; }
    public required string Source { get; init; }
    public Confidence Confidence { get; set; }

    public IReadOnlyList<EvidenceRecord> Evidence => _evidence;
    public IReadOnlyList<string> Tags => _tags;
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    /// <summary>
    /// Attaches a piece of evidence. An entity with zero evidence cannot legitimately
    /// claim a Status stronger than Unknown — callers are expected to honor that,
    /// this method only stores the record.
    /// </summary>
    public void AddEvidence(EvidenceRecord evidence) => _evidence.Add(evidence);

    public void AddTag(string tag)
    {
        if (!_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(tag);
        }
    }

    public void SetMetadata(string key, string value) => _metadata[key] = value;
}
