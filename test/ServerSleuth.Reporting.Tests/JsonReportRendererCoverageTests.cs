using System.Text.Json;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Coverage and coverage-warning rendering — see skill.md (Phase 9A) §5, §13.</summary>
public class JsonReportRendererCoverageTests
{
    private static JsonDocument Render(AggregateDiscoveryResult? discovery)
    {
        var report = TestPipeline.Run([], discovery);
        var result = new JsonReportRenderer().Render(report);
        return JsonDocument.Parse(result.Content);
    }

    private static AggregateDiscoveryResult Aggregate(params DiscoveryResult[] scannerResults) => new()
    {
        Entities = [],
        Errors = scannerResults.SelectMany(r => r.Errors).ToList(),
        ScannerResults = scannerResults,
        ScannerStatuses = scannerResults.ToDictionary(r => r.ScannerId, r => r.Status, StringComparer.Ordinal)
    };

    [Fact]
    public void NoDiscoverySupplied_RendersUnknownCoverage_NoWarnings()
    {
        var json = Render(null);
        Assert.Equal("Unknown", json.RootElement.GetProperty("Coverage").GetString());
        Assert.Empty(json.RootElement.GetProperty("CoverageWarnings").EnumerateArray());
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

        var json = Render(discovery);
        Assert.Equal("Limited", json.RootElement.GetProperty("Coverage").GetString());

        var warning = Assert.Single(json.RootElement.GetProperty("CoverageWarnings").EnumerateArray());
        Assert.Equal("windows-iis-scanner", warning.GetProperty("ScannerId").GetString());
        Assert.Equal("AccessDenied", warning.GetProperty("ScannerStatus").GetString());
        Assert.Equal("Windows", warning.GetProperty("AffectedPlatform").GetString());
        Assert.Contains("Access to IIS configuration was denied.", warning.GetProperty("Reason").GetString());
    }

    [Fact]
    public void PartiallySupportedScanner_RendersPartialCoverage()
    {
        var discovery = Aggregate(new DiscoveryResult { ScannerId = "linux-package-scanner", Status = ScannerStatus.PartiallySupported });
        var json = Render(discovery);
        Assert.Equal("Partial", json.RootElement.GetProperty("Coverage").GetString());
    }

    [Fact]
    public void AllSupportedScanners_RenderCompleteCoverage()
    {
        var discovery = Aggregate(
            new DiscoveryResult { ScannerId = "windows-service-scanner", Status = ScannerStatus.Supported },
            new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.NotApplicable });

        var json = Render(discovery);
        Assert.Equal("Complete", json.RootElement.GetProperty("Coverage").GetString());
        Assert.Empty(json.RootElement.GetProperty("CoverageWarnings").EnumerateArray());
    }
}
