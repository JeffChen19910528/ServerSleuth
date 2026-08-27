using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Analysis.Tests.Migration.Consolidation;

/// <summary>Negative fixtures for Phase 8C consolidation — see skill.md (Phase 8C) §19. Nothing
/// should disappear during consolidation.</summary>
public class NegativeMigrationReportFixtureTests
{
    private static ServerMigrationAssessmentReport Build(List<DiscoveryEntity> entities, AggregateDiscoveryResult? discovery = null)
    {
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);
        return ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan, discovery);
    }

    // 1. Empty discovery -> empty, valid report.
    [Fact]
    public void EmptyDiscovery_ProducesEmptyValidReport()
    {
        var report = Build([]);

        Assert.Equal(MigrationStatus.Ready, report.ServerSummary.OverallMigrationStatus);
        Assert.Equal(AggregateSeverity.None, report.ServerSummary.OverallRiskSeverity);
        Assert.Empty(report.ApplicationAssessments);
        Assert.Empty(report.Actions);
    }

    // 2. No findings -> Ready, nothing lost.
    [Fact]
    public void NoFindings_IsReady()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var report = Build([runtime]);

        Assert.Equal(MigrationStatus.Ready, report.ServerSummary.OverallMigrationStatus);
        Assert.Empty(report.ServerLevelIssues);
    }

    // 3. Info-only -> Ready, issue still visible as server-level (no boundary).
    [Fact]
    public void InfoOnlyFindings_StillVisibleAsServerLevelIssue()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: ["EnvVar: APP_HOME"]);
        var report = Build([config]);

        Assert.Equal(MigrationStatus.Ready, report.ServerSummary.OverallMigrationStatus);
        Assert.NotEmpty(report.ServerLevelIssues);
    }

    // 4. Low-only -> Ready.
    [Fact]
    public void LowOnlyFindings_IsReady()
    {
        var config = EntityFactory.Configuration("/etc/app/app.conf", dependencyReferences: ["UnixSocket: /var/run/app.sock"]);
        var report = Build([config]);

        Assert.Equal(MigrationStatus.Ready, report.ServerSummary.OverallMigrationStatus);
    }

    // 5. Medium dependency -> ReadyWithConditions, dependency grouped and visible.
    [Fact]
    public void MediumExternalDependency_IsReadyWithConditions_DependencyVisible()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "AppDb");

        var report = Build([config]);

        Assert.Equal(MigrationStatus.ReadyWithConditions, report.ServerSummary.OverallMigrationStatus);
        Assert.Contains(report.Dependencies, g => g.Type == MigrationDependencyType.Database);
    }

    // 6. High certificate -> NeedsRemediation, action + checks present.
    [Fact]
    public void HighCertificateIssue_IsNeedsRemediation_WithActionAndChecks()
    {
        var expiring = EntityFactory.Certificate("neg6.example.com", "NEGCERT6", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var report = Build([expiring]);

        Assert.Equal(MigrationStatus.NeedsRemediation, report.ServerSummary.OverallMigrationStatus);
        Assert.Single(report.Actions);
        Assert.NotEmpty(report.PreMigrationChecks);
        Assert.NotEmpty(report.PostMigrationChecks);
    }

    // 7. Critical missing binary -> Blocked.
    [Fact]
    public void CriticalMissingBinary_IsBlocked()
    {
        var service = EntityFactory.Service("Neg7Svc", @"D:\Neg7\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg7\svc.exe", notFound: true);
        var report = Build([service, missingExe]);

        Assert.Equal(MigrationStatus.Blocked, report.ServerSummary.OverallMigrationStatus);
        Assert.Equal(1, report.ServerSummary.BlockedApplicationCount);
    }

    // 8. Shared binary across 3 boundaries -> one logical dependency, 3 boundaries.
    [Fact]
    public void SharedBinaryAcrossThreeBoundaries_OneLogicalDependency()
    {
        var serviceA = EntityFactory.Service("Neg8A", @"D:\Neg8\host.exe");
        var serviceB = EntityFactory.Service("Neg8B", @"D:\Neg8\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Neg8\Neg8C", @"D:\Neg8\host.exe");
        var exe = EntityFactory.Dll(@"D:\Neg8\host.exe");

        var report = Build([serviceA, serviceB, taskC, exe]);

        var shared = Assert.Single(report.SharedInfrastructure);
        Assert.Equal(3, shared.AffectedBoundaryIds.Count);
    }

    // 9. Same-name different-path binaries -> no false shared dependency.
    [Fact]
    public void SameNamedDifferentPathBinaries_NoFalseSharedDependency()
    {
        var serviceA = EntityFactory.Service("Neg9A", @"D:\Neg9A\bin\Common.exe");
        var exeA = EntityFactory.Dll(@"D:\Neg9A\bin\Common.exe");
        var serviceB = EntityFactory.Service("Neg9B", @"D:\Neg9B\bin\Common.exe");
        var exeB = EntityFactory.Dll(@"D:\Neg9B\bin\Common.exe");

        var report = Build([serviceA, exeA, serviceB, exeB]);

        Assert.Empty(report.SharedInfrastructure);
    }

    // 10. Server-only issue -> visible in ServerLevelIssues, not forced into an application.
    [Fact]
    public void ServerOnlyIssue_VisibleAtServerLevel_NotForcedIntoApplication()
    {
        var expiring = EntityFactory.Certificate("neg10.example.com", "NEGCERT10", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var report = Build([expiring]);

        Assert.Empty(report.ApplicationAssessments);
        Assert.Contains(report.ServerLevelIssues, i => i.RuleId == "RR5-CertificateExpiry");
    }

    // 11. Partial scanner support -> Coverage = Partial, migration status unaffected.
    [Fact]
    public void PartialScannerSupport_CoveragePartial_StatusUnaffected()
    {
        var discovery = new AggregateDiscoveryResult
        {
            Entities = [],
            Errors = [],
            ScannerResults = [new DiscoveryResult { ScannerId = "windows-com-scanner", Status = ScannerStatus.PartiallySupported }],
            ScannerStatuses = new Dictionary<string, ScannerStatus> { ["windows-com-scanner"] = ScannerStatus.PartiallySupported }
        };

        var report = Build([], discovery);

        Assert.Equal(AssessmentCoverage.Partial, report.Coverage);
        Assert.Equal(MigrationStatus.Ready, report.ServerSummary.OverallMigrationStatus);
    }

    // 12. AccessDenied scanner -> Coverage = Limited, remains visible as a warning.
    [Fact]
    public void AccessDeniedScanner_CoverageLimited_WarningVisible()
    {
        var discovery = new AggregateDiscoveryResult
        {
            Entities = [],
            Errors = [],
            ScannerResults = [new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.AccessDenied }],
            ScannerStatuses = new Dictionary<string, ScannerStatus> { ["windows-iis-scanner"] = ScannerStatus.AccessDenied }
        };

        var report = Build([], discovery);

        Assert.Equal(AssessmentCoverage.Limited, report.Coverage);
        Assert.Contains(report.CoverageWarnings, w => w.ScannerId == "windows-iis-scanner");
    }

    // 13. Expected orphan runtime -> no false action/dependency.
    [Fact]
    public void ExpectedOrphanRuntime_NoFalseActionOrIssue()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var report = Build([runtime]);

        Assert.Empty(report.Actions);
        Assert.Empty(report.ServerLevelIssues);
    }

    // 14. Expected orphan certificate -> no false action/dependency.
    [Fact]
    public void ExpectedOrphanCertificate_NoFalseActionOrIssue()
    {
        var certificate = EntityFactory.Certificate("neg14.example.com", "NEGORPHAN14", validTo: DateTimeOffset.UtcNow.AddYears(2));
        var report = Build([certificate]);

        Assert.Empty(report.Actions);
        Assert.Empty(report.ServerLevelIssues);
    }

    // 15. No ApplicationBoundary but server-level findings -> still fully visible.
    [Fact]
    public void NoApplicationBoundary_ButServerLevelFindings_StillVisible()
    {
        var expiring = EntityFactory.Certificate("neg15.example.com", "NEGCERT15", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var report = Build([expiring]);

        Assert.Empty(report.ApplicationAssessments);
        Assert.NotEmpty(report.ServerLevelIssues);
        Assert.NotEmpty(report.Actions);
        Assert.NotEmpty(report.PreMigrationChecks);
        Assert.NotEmpty(report.PostMigrationChecks);
    }
}
