using System.Text.Json;
using ServerSleuth.Reporting.Export;
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
        var report = TestPipeline.Run(entities);

        // 2 & 3. Render JSON and HTML from the same report instance.
        var bundle = ReportArtifactFactory.CreateBundle(report);

        // 4. Export both.
        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        // 5. Verify both exist.
        Assert.True(result.Success);
        Assert.True(System.IO.File.Exists(result.Json.OutputPath));
        Assert.True(System.IO.File.Exists(result.Html.OutputPath));

        // 6. Verify their content corresponds to the same report.
        var jsonContent = System.IO.File.ReadAllText(result.Json.OutputPath!);
        var htmlContent = System.IO.File.ReadAllText(result.Html.OutputPath!);

        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var jsonStatus = jsonDoc.RootElement.GetProperty("Server").GetProperty("OverallMigrationStatus").GetString();
        var jsonBlockedCount = jsonDoc.RootElement.GetProperty("Server").GetProperty("BlockingIssueCount").GetInt32();

        Assert.Equal(report.ServerSummary.OverallMigrationStatus.ToString(), jsonStatus);
        Assert.Equal(report.ServerSummary.BlockingIssueCount, jsonBlockedCount);

        var expectedBadge = $"badge status-{CssKebabCase(jsonStatus!)}";
        Assert.Contains(expectedBadge, htmlContent, StringComparison.Ordinal);

        // The shared host.exe dependency ID must appear (in each format's own valid encoding)
        // identically in both — parsed back out of JSON to avoid a false negative from JSON's
        // own backslash-escaping of Windows paths.
        var sharedDependencyId = report.SharedInfrastructure.Single().DependencyId;
        var jsonDependencyId = jsonDoc.RootElement.GetProperty("SharedInfrastructure").EnumerateArray().Single().GetProperty("DependencyId").GetString();
        Assert.Equal(sharedDependencyId, jsonDependencyId);
        Assert.Contains(sharedDependencyId, htmlContent, StringComparison.Ordinal);
    }

    private static string CssKebabCase(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
}
