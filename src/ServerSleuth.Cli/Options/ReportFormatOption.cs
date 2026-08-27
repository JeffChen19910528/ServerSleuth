namespace ServerSleuth.Cli.Options;

/// <summary>Which report format(s) <c>scan --format</c> should produce — see skill.md
/// (Phase 10A) §6, §11.</summary>
public enum ReportFormatOption
{
    Json,
    Html,
    Both
}
