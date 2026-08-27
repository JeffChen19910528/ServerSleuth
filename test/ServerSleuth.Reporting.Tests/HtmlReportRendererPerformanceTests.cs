using System.Diagnostics;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Synthetic-scale HTML rendering performance test — skill.md (Phase 9B) §26: >=10,000 entities,
/// 1,000 applications, 10,000 issues, 5,000 dependencies, 10,000 actions, 20,000 verification
/// checks, rendered entirely in memory, target &lt;10s. See
/// <see cref="SyntheticLargeReportBuilder"/> for the fixture (shared with the JSON renderer's
/// own equivalent performance test).
/// </summary>
public class HtmlReportRendererPerformanceTests
{
    [Fact]
    public void Render_TenThousandEntities_OneThousandApplications_LargeScale_CompletesUnderTenSeconds()
    {
        var report = SyntheticLargeReportBuilder.Build();

        var stopwatch = Stopwatch.StartNew();
        var result = new HtmlReportRenderer().Render(report);
        stopwatch.Stop();

        Assert.False(string.IsNullOrEmpty(result.Content));
        Assert.Contains("<!doctype html>", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"HtmlReportRenderer.Render took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected < 10s.");
    }
}
