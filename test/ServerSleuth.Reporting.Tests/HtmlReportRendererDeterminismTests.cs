using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Deterministic rendering — see skill.md (Phase 9B) §22. No timestamp/GUID/random
/// ordering by default; the same report rendered repeatedly produces byte-identical HTML.</summary>
public class HtmlReportRendererDeterminismTests
{
    private static List<DiscoveryEntity> BuildScenario()
    {
        var serviceA = EntityFactory.Service("DetA", @"D:\Det\host.exe");
        var serviceB = EntityFactory.Service("DetB", @"D:\Det\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Det\DetC", @"D:\Det\host.exe");
        var exe = EntityFactory.Dll(@"D:\Det\host.exe");
        var expiring = EntityFactory.Certificate("det.example.com", "DETCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));

        return [serviceA, serviceB, taskC, exe, expiring];
    }

    [Fact]
    public void DefaultRenderer_ProducesNoTimestamp()
    {
        var report = TestPipeline.Run(BuildScenario());
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("Generated:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingTheSameReport_TwiceProducesByteIdenticalHtml()
    {
        var report = TestPipeline.Run(BuildScenario());
        var renderer = new HtmlReportRenderer();

        var first = renderer.Render(report);
        var second = renderer.Render(report);

        Assert.Equal(first.Content, second.Content, StringComparer.Ordinal);
    }

    [Fact]
    public void RenderingTheSameReport_TenTimesProducesIdenticalHtml()
    {
        var report = TestPipeline.Run(BuildScenario());
        var renderer = new HtmlReportRenderer();

        var results = Enumerable.Range(0, 10).Select(_ => renderer.Render(report).Content).ToList();

        Assert.All(results, r => Assert.Equal(results[0], r, StringComparer.Ordinal));
    }

    [Fact]
    public void OptInGeneratedTimestamp_AppearsOnlyWhenExplicitlySupplied()
    {
        var report = TestPipeline.Run(BuildScenario());
        var fixedTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var html = new HtmlReportRenderer(generatedAt: fixedTime).Render(report).Content;

        Assert.Contains("Generated:", html, StringComparison.Ordinal);
        Assert.Contains("2026-01-01", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OptInTimestamp_StillProducesIdenticalOutput_ForTheSameFixedValue()
    {
        var report = TestPipeline.Run(BuildScenario());
        var fixedTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = new HtmlReportRenderer(generatedAt: fixedTime).Render(report).Content;
        var second = new HtmlReportRenderer(generatedAt: fixedTime).Render(report).Content;

        Assert.Equal(first, second, StringComparer.Ordinal);
    }
}
