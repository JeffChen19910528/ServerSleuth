using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Coverage and coverage-warning rendering — see skill.md (Phase 9B) §8.</summary>
public class HtmlReportRendererCoverageTests
{
    private static string Render(AggregateDiscoveryResult? discovery)
    {
        var report = TestPipeline.Run([], discovery);
        return new HtmlReportRenderer().Render(report).Content;
    }

    private static AggregateDiscoveryResult Aggregate(params DiscoveryResult[] scannerResults) => new()
    {
        Entities = [],
        Errors = scannerResults.SelectMany(r => r.Errors).ToList(),
        ScannerResults = scannerResults,
        ScannerStatuses = scannerResults.ToDictionary(r => r.ScannerId, r => r.Status, StringComparer.Ordinal)
    };

    [Fact]
    public void NoDiscoverySupplied_RendersUnknownCoverage()
    {
        var html = Render(null);
        Assert.Contains("badge coverage-unknown", html, StringComparison.Ordinal);
        Assert.Contains("No coverage warnings.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AccessDeniedScanner_RendersLimitedCoverage_WithVisibleWarning()
    {
        var discovery = Aggregate(new DiscoveryResult
        {
            ScannerId = "windows-iis-scanner",
            Status = ScannerStatus.AccessDenied,
            Errors = [new DiscoveryError { ScannerId = "windows-iis-scanner", Message = "Access to IIS configuration was denied.", IsPermissionFailure = true }]
        });

        var html = Render(discovery);
        Assert.Contains("badge coverage-limited", html, StringComparison.Ordinal);
        Assert.Contains("windows-iis-scanner", html, StringComparison.Ordinal);
        Assert.Contains("Access to IIS configuration was denied.", html, StringComparison.Ordinal);
        Assert.Contains("Windows", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialCoverage_RendersIndependentlyOfMigrationStatus()
    {
        var discovery = Aggregate(new DiscoveryResult { ScannerId = "linux-package-scanner", Status = ScannerStatus.PartiallySupported });
        var html = Render(discovery);

        Assert.Contains("badge coverage-partial", html, StringComparison.Ordinal);
        Assert.Contains("badge status-ready\"", html, StringComparison.Ordinal);
    }
}
