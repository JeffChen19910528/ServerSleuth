using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Reporting.Json.Dto;

namespace ServerSleuth.Reporting.Json;

/// <summary>
/// Renders a <see cref="ServerMigrationAssessmentReport"/> to a deterministic, UTF-8-safe JSON
/// document using <c>System.Text.Json</c> — see skill.md (Phase 9A) §4, §8-9. Serializes the DTO
/// tree <see cref="ReportDtoMapper"/> produces, never the domain model directly, so the JSON
/// contract's shape stays fully controlled at one boundary (§6).
///
/// Deterministic by construction (§8): the DTO tree is built from the source report's own
/// already-ordinal-sorted collections (never re-sorted here), enum values serialize as fixed
/// strings (never a dictionary-order-dependent representation), and <c>JsonSerializer</c> writes
/// object properties in the DTO's declared order — the same input always produces byte-identical
/// output.
/// </summary>
public sealed class JsonReportRenderer : IReportRenderer
{
    /// <summary>
    /// <see cref="UnicodeRanges.All"/> keeps Unicode text (e.g. Traditional Chinese application
    /// names/paths) readable as literal characters instead of the default <c>\uXXXX</c> escaping
    /// — both are equally valid, uncorrupted JSON/UTF-8 (skill.md §9), but literal characters are
    /// what a human reading the report actually expects. JSON-structural characters (quotes,
    /// backslashes, control characters) are still always escaped correctly by the JSON writer
    /// itself regardless of this encoder — this setting only affects string CONTENT encoding.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public ReportFormat Format => ReportFormat.Json;

    public ReportRenderResult Render(ServerMigrationAssessmentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var dto = ReportDtoMapper.ToDto(report);
        var json = JsonSerializer.Serialize(dto, Options);

        return new ReportRenderResult
        {
            Format = ReportFormat.Json,
            Content = json,
            Encoding = Encoding.UTF8
        };
    }
}
