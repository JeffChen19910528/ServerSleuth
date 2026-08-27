namespace ServerSleuth.Reporting.Export;

/// <summary>
/// The JSON and HTML artifacts rendered from the SAME <c>ServerMigrationAssessmentReport</c>
/// instance — see skill.md (Phase 9C) §9-10. <see cref="ReportArtifactFactory.CreateBundle"/> is
/// the only place a <see cref="ReportBundle"/> is constructed, and it renders both formats from
/// one report instance in one call — there is no code path that could build a bundle whose two
/// artifacts describe different assessments.
/// </summary>
public sealed record ReportBundle
{
    public required ReportArtifact Json { get; init; }
    public required ReportArtifact Html { get; init; }
}
