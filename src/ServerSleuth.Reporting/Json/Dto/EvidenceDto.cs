namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>
/// Mirrors <see cref="ServerSleuth.Core.Evidence.EvidenceRecord"/> exactly — Type/Location/
/// Detail/CapturedAt, nothing more, nothing renamed. <c>Detail</c> is already redaction-safe by
/// construction (every <c>EvidenceRecord</c> in the codebase is built with secret values already
/// stripped/replaced — see <c>ISecretRedactor</c>, Phase 2); this DTO does not re-implement that
/// redaction, it only refuses to add any NEW field beyond what <c>EvidenceRecord</c> itself
/// already exposes (skill.md (Phase 9A) §6: "solve it at the reporting DTO/contract boundary").
/// </summary>
public sealed record EvidenceDto
{
    public required string Type { get; init; }
    public required string Location { get; init; }
    public string? Detail { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
}
