using System.Text.Json;
using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>JSON/HTML same-assessment consistency — see skill.md (Phase 9C) §10: creates one
/// assessment, renders JSON, renders HTML, exports both, verifies both exist, and verifies their
/// content corresponds to the same report — without re-running analysis between formats.</summary>
public class ReportBundleConsistencyTests
{
    [Fact]
    public void JsonAndHtmlArtifacts_RepresentTheSameAssessment()
    {
        using var temp = new TempDirectory();

        // 1. Create one assessment.
        var serviceA = EntityFactory.Service("ConsA", @"D:\Cons\host.exe");
        var serviceB = EntityFactory.Service("ConsB", @"D:\Cons\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Cons\ConsC", @"D:\Cons\host.exe");
        var exe = EntityFactory.Dll(@"D:\Cons\host.exe");
        var expiring = EntityFactory.Certificate("cons.example.com", "CONSCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));

        var entities = new List<Core.Models.DiscoveryEntity> { serviceA, serviceB, taskC, exe, expiring };
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);

        // 2 & 3. Render JSON and HTML from the same report/discovery/boundaries instances.
        var jsonResult = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: []).Render(report);
        var htmlResult = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: []).Render(report);
        var bundle = new ReportBundle
        {
            Json = ReportArtifactFactory.FromRenderResult(jsonResult, ReportArtifactFactory.DefaultJsonFileName),
            Html = ReportArtifactFactory.FromRenderResult(htmlResult, ReportArtifactFactory.DefaultHtmlFileName)
        };

        // 4. Export both.
        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        // 5. Verify both exist.
        Assert.True(result.Success);
        Assert.True(System.IO.File.Exists(result.Json.OutputPath));
        Assert.True(System.IO.File.Exists(result.Html.OutputPath));

        // 6. Verify their content corresponds to the same report/discovery: the JSON still
        // carries the full internal Migration status, while the HTML shows the same underlying
        // deployed applications (ConsA/ConsB/ConsC — three separate anchors sharing one exe,
        // never merged) without any Risk/Migration status badge.
        var jsonContent = System.IO.File.ReadAllText(result.Json.OutputPath!);
        var htmlContent = System.IO.File.ReadAllText(result.Html.OutputPath!);

        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var jsonStatus = jsonDoc.RootElement.GetProperty("Server").GetProperty("OverallMigrationStatus").GetString();
        Assert.Equal(report.ServerSummary.OverallMigrationStatus.ToString(), jsonStatus);

        Assert.DoesNotContain("badge status-", htmlContent, StringComparison.Ordinal);
        Assert.Contains(">ConsA<", htmlContent, StringComparison.Ordinal);
        Assert.Contains(">ConsB<", htmlContent, StringComparison.Ordinal);
        Assert.Contains("ConsC", htmlContent, StringComparison.Ordinal);
    }
}
