namespace ServerSleuth.Reporting;

/// <summary>
/// Closed set of output formats <see cref="IReportRenderer"/> implementations produce — see
/// skill.md (Phase 9A) §3. <see cref="Json"/> (Phase 9A) and <see cref="Html"/> (Phase 9B) exist
/// today; PDF remains explicitly deferred (Phase 9B §32) and deliberately has no placeholder
/// member here — adding one unused today would be exactly the kind of unnecessary abstraction §3
/// warns against.
/// </summary>
public enum ReportFormat
{
    Json,
    Html
}
