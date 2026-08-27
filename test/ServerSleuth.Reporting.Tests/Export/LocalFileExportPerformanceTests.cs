using System.Diagnostics;
using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>
/// Synthetic-scale render+export performance test — skill.md (Phase 9C) §19: >=10,000 entities,
/// 1,000 applications, 10,000 issues, 5,000 dependencies, 10,000 actions, 20,000 verification
/// checks, target &lt;10s for render+export combined. Reuses
/// <see cref="SyntheticLargeReportBuilder"/> (shared with the Phase 9A/9B renderer performance
/// tests).
/// </summary>
public class LocalFileExportPerformanceTests
{
    [Fact]
    public void RenderAndExport_TenThousandEntities_OneThousandApplications_LargeScale_CompletesUnderTenSeconds()
    {
        using var temp = new TempDirectory();
        var report = SyntheticLargeReportBuilder.Build();

        var stopwatch = Stopwatch.StartNew();
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true);
        stopwatch.Stop();

        Assert.True(result.Success);
        Assert.True(System.IO.File.Exists(result.Json.OutputPath));
        Assert.True(System.IO.File.Exists(result.Html.OutputPath));
        Assert.True(System.IO.File.Exists(result.Manifest!.OutputPath));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Render+export took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected < 10s.");
    }
}
