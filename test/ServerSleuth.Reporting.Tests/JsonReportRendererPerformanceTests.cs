using System.Diagnostics;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Synthetic-scale JSON rendering performance test — skill.md (Phase 9A) §16: >=10,000 entities,
/// 1,000 applications, 10,000 issues, 5,000 dependencies, 10,000 actions, 20,000 verification
/// checks, rendered entirely in memory, target &lt;10s. See
/// <see cref="SyntheticLargeReportBuilder"/> for the fixture (shared with the HTML renderer's
/// own equivalent performance test).
/// </summary>
public class JsonReportRendererPerformanceTests
{
    [Fact]
    public void Render_TenThousandEntities_OneThousandApplications_LargeScale_CompletesUnderTenSeconds()
    {
        var report = SyntheticLargeReportBuilder.Build();

        var stopwatch = Stopwatch.StartNew();
        var result = new JsonReportRenderer().Render(report);
        stopwatch.Stop();

        Assert.False(string.IsNullOrEmpty(result.Content));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"JsonReportRenderer.Render took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected < 10s.");
    }
}
