using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Analysis.Tests.Migration.Consolidation;

/// <summary>
/// <see cref="AssessmentCoverage"/> derivation and <see cref="CoverageWarning"/> generation — see
/// skill.md (Phase 8C) §11-13. Coverage is derived solely from an already-produced
/// <see cref="AggregateDiscoveryResult"/>, never from anything Risk/Migration Analysis computed.
/// </summary>
public class CoverageTests
{
    private static ServerMigrationAssessmentReport BuildReport(AggregateDiscoveryResult? discovery)
    {
        var (result, context) = RiskPipeline.Run([]);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        return ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan, discovery);
    }

    private static AggregateDiscoveryResult Aggregate(params DiscoveryResult[] scannerResults) => new()
    {
        Entities = [],
        Errors = scannerResults.SelectMany(r => r.Errors).ToList(),
        ScannerResults = scannerResults,
        ScannerStatuses = scannerResults.ToDictionary(r => r.ScannerId, r => r.Status, StringComparer.Ordinal)
    };

    [Fact]
    public void NoDiscoveryResultSupplied_IsUnknown()
    {
        var report = BuildReport(null);
        Assert.Equal(AssessmentCoverage.Unknown, report.Coverage);
    }

    [Fact]
    public void EmptyScannerResults_IsUnknown()
    {
        var report = BuildReport(Aggregate());
        Assert.Equal(AssessmentCoverage.Unknown, report.Coverage);
    }

    [Fact]
    public void AllScannersSupported_IsComplete()
    {
        var discovery = Aggregate(
            new DiscoveryResult { ScannerId = "windows-service-scanner", Status = ScannerStatus.Supported },
            new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.NotApplicable },
            new DiscoveryResult { ScannerId = "linux-container-scanner", Status = ScannerStatus.NotInstalled });

        var report = BuildReport(discovery);
        Assert.Equal(AssessmentCoverage.Complete, report.Coverage);
        Assert.Empty(report.CoverageWarnings);
    }

    [Fact]
    public void OnePartiallySupportedScanner_IsPartial()
    {
        var discovery = Aggregate(
            new DiscoveryResult { ScannerId = "windows-service-scanner", Status = ScannerStatus.Supported },
            new DiscoveryResult { ScannerId = "windows-com-scanner", Status = ScannerStatus.PartiallySupported });

        var report = BuildReport(discovery);
        Assert.Equal(AssessmentCoverage.Partial, report.Coverage);

        var warning = Assert.Single(report.CoverageWarnings);
        Assert.Equal("windows-com-scanner", warning.ScannerId);
        Assert.Equal(ScannerStatus.PartiallySupported, warning.ScannerStatus);
        Assert.Equal("Windows", warning.AffectedPlatform);
    }

    [Fact]
    public void AccessDeniedScanner_IsLimited_AndRemainsVisibleAsWarning()
    {
        var discovery = Aggregate(
            new DiscoveryResult
            {
                ScannerId = "windows-iis-scanner",
                Status = ScannerStatus.AccessDenied,
                Errors = [new DiscoveryError { ScannerId = "windows-iis-scanner", Message = "Access to IIS configuration was denied.", IsPermissionFailure = true }]
            });

        var report = BuildReport(discovery);
        Assert.Equal(AssessmentCoverage.Limited, report.Coverage);

        var warning = Assert.Single(report.CoverageWarnings);
        Assert.Equal("windows-iis-scanner", warning.ScannerId);
        Assert.Equal(ScannerStatus.AccessDenied, warning.ScannerStatus);
        Assert.Contains("Access to IIS configuration was denied.", warning.Reason);
        Assert.Contains("Access to IIS configuration was denied.", warning.Evidence);
    }

    [Fact]
    public void FailedScannerOutranksPartiallySupported_StillLimited()
    {
        var discovery = Aggregate(
            new DiscoveryResult { ScannerId = "linux-package-scanner", Status = ScannerStatus.PartiallySupported },
            new DiscoveryResult { ScannerId = "linux-kubernetes-scanner", Status = ScannerStatus.Failed });

        var report = BuildReport(discovery);
        Assert.Equal(AssessmentCoverage.Limited, report.Coverage);
        Assert.Equal(2, report.CoverageWarnings.Count);
    }

    [Fact]
    public void CoverageNeverAffectsMigrationStatus_EvenWhenLimited()
    {
        // No findings in this scenario at all (empty entity list) — MigrationStatus must stay
        // Ready regardless of how poor the (synthetic) discovery coverage was.
        var discovery = Aggregate(new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.AccessDenied });

        var report = BuildReport(discovery);
        Assert.Equal(AssessmentCoverage.Limited, report.Coverage);
        Assert.Equal(MigrationStatus.Ready, report.ServerSummary.OverallMigrationStatus);
    }

    [Fact]
    public void CoverageWarnings_AreOrdinalSortedByScannerId()
    {
        var discovery = Aggregate(
            new DiscoveryResult { ScannerId = "windows-service-scanner", Status = ScannerStatus.Failed },
            new DiscoveryResult { ScannerId = "linux-container-scanner", Status = ScannerStatus.AccessDenied },
            new DiscoveryResult { ScannerId = "windows-com-scanner", Status = ScannerStatus.PartiallySupported });

        var report = BuildReport(discovery);
        var ids = report.CoverageWarnings.Select(w => w.ScannerId).ToList();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
    }
}
