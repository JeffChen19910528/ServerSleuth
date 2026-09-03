using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// The Server Deployment Inventory report deliberately no longer renders a Coverage section
/// (scanner coverage/warnings are assessment machinery, not "what is deployed") — this test
/// confirms that removal holds regardless of scanner access-denied/partial-coverage state.
/// </summary>
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
    public void CoverageSection_NeverRendered_RegardlessOfScannerStatus()
    {
        var discovery = Aggregate(new DiscoveryResult
        {
            ScannerId = "windows-iis-scanner",
            Status = ScannerStatus.AccessDenied,
            Errors = [new DiscoveryError { ScannerId = "windows-iis-scanner", Message = "Access to IIS configuration was denied.", IsPermissionFailure = true }]
        });

        var html = Render(discovery);
        Assert.DoesNotContain("badge coverage-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"coverage\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Access to IIS configuration was denied.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDiscoverySupplied_StillRendersValidDocument()
    {
        var html = Render(null);
        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
    }
}
