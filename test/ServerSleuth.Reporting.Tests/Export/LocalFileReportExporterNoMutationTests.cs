using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>No-mutation — see skill.md (Phase 9C) §16. Export never modifies the artifact,
/// bundle, or the source <c>ServerMigrationAssessmentReport</c> it was ultimately rendered from.</summary>
public class LocalFileReportExporterNoMutationTests
{
    [Fact]
    public void Export_NeverMutatesTheArtifactOrBundle()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var jsonContentBefore = bundle.Json.Content;
        var htmlContentBefore = bundle.Html.Content;
        var jsonLengthBefore = bundle.Json.ContentLength;

        new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        Assert.Same(jsonContentBefore, bundle.Json.Content);
        Assert.Same(htmlContentBefore, bundle.Html.Content);
        Assert.Equal(jsonLengthBefore, bundle.Json.ContentLength);
    }

    [Fact]
    public void Export_NeverMutatesTheUnderlyingReport()
    {
        var report = TestPipeline.Run([]);
        var issuesBefore = report.Assessment.Server.Issues.Select(i => i.IssueId).ToList();
        var actionsBefore = report.Plan.Actions.Select(a => a.ActionId).ToList();

        using var temp = new TempDirectory();
        var bundle = ReportArtifactFactory.CreateBundle(report);
        new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        Assert.Equal(issuesBefore, report.Assessment.Server.Issues.Select(i => i.IssueId));
        Assert.Equal(actionsBefore, report.Plan.Actions.Select(a => a.ActionId));
    }

    [Fact]
    public void RepeatedExports_OfTheSameArtifactObject_ProduceIdenticalContentEachTime()
    {
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var exporter = new LocalFileReportExporter();

        exporter.Export(bundle.Json, tempA.Path);
        exporter.Export(bundle.Json, tempB.Path);

        Assert.Equal(
            File.ReadAllText(Path.Combine(tempA.Path, "report.json")),
            File.ReadAllText(Path.Combine(tempB.Path, "report.json")),
            StringComparer.Ordinal);
    }
}
