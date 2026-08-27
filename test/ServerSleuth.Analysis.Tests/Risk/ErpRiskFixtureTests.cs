using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

/// <summary>
/// End-to-end synthetic legacy-enterprise-server scenario (skill.md Phase 7A §30), extending
/// the shape used by <c>RealisticErpFixtureTests</c>/<c>DependencyExpansionEngineTests</c> with
/// eight deliberate migration risks, run through the full Discovery→Correlation→Boundary→
/// Expansion→Validation→Risk pipeline via <see cref="RiskPipeline"/>. Confirms each intentional
/// risk is detected with the right category/severity, and that unrelated/healthy entities in
/// the same fixture never produce a finding (no false positives).
/// </summary>
public class ErpRiskFixtureTests
{
    private static (RiskAnalysisResult Result, Fixture Fixture) BuildAndAnalyze()
    {
        var site = EntityFactory.Site("ERP", @"D:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP\Web", poolId: pool.Id, siteId: site.Id);

        // Risk 2: missing imported native dependency (Binary --IMPORTS--> Binary NotFound).
        var webDll = EntityFactory.Dll(@"D:\ERP\Web\ERP.Web.dll", referencedBy: [app.Id], importsCsv: "VendorImport.dll");
        var missingImportDll = EntityFactory.Dll(@"D:\ERP\Web\VendorImport.dll", notFound: true);

        // Risks 4, 5, 6, 8: one configuration file that is a SQL Server dependency, a UNC file
        // share dependency, an explicit (unsatisfied) net8.0 requirement, and partially
        // inaccessible, all at once — a realistic combination, not four separate files.
        var appConfig = EntityFactory.Configuration(@"D:\ERP\Web\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["RuntimeVersion: net8.0"]);
        appConfig.SetMetadata("ParseStatus", "AccessDenied");
        appConfig.SetMetadata("Database0.Type", "SqlServer");
        appConfig.SetMetadata("Database0.Host", "DB01");
        appConfig.SetMetadata("Database0.Port", "1433");
        appConfig.SetMetadata("Database0.Name", "ERP");
        appConfig.SetMetadata("NetworkPath0.Server", "FILESERVER");
        appConfig.SetMetadata("NetworkPath0.Share", "ERPData");

        // Risk 6 continued: only .NET 6 and .NET 10 are installed — no .NET 8 anywhere.
        var runtime6 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.30");
        var runtime10 = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");

        // Risk 3: HTTPS certificate bound to the site, expiring in 10 days (High window).
        EntityFactory.SetBinding(site, 0, "EXPIRING123");
        var expiringCert = EntityFactory.Certificate("erp.example.com", "EXPIRING123", validTo: DateTimeOffset.UtcNow.AddDays(10));

        // Risk 1: the ERP worker service's executable is missing on disk.
        var service = EntityFactory.Service("ERPWorker", @"D:\ERP\Worker\ERPWorker.exe");
        var missingWorkerExe = EntityFactory.Dll(@"D:\ERP\Worker\ERPWorker.exe", notFound: true);

        // Risk 7: three independent workloads share one executable (found on disk).
        var batchA = EntityFactory.Service("BatchA", @"D:\ERP\Shared\host.exe");
        var batchB = EntityFactory.Service("BatchB", @"D:\ERP\Shared\host.exe");
        var batchC = EntityFactory.ScheduledTask(@"\ERP\BatchC", @"D:\ERP\Shared\host.exe");
        var sharedHostExe = EntityFactory.Dll(@"D:\ERP\Shared\host.exe");

        // Unrelated/healthy entities — none of these should ever produce a finding.
        var healthyDll = EntityFactory.Dll(@"D:\ERP\Web\Healthy.dll", referencedBy: [app.Id]);
        var healthyCert = EntityFactory.Certificate("unused.example.com", "HEALTHY999", validTo: DateTimeOffset.UtcNow.AddYears(2));

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app,
            webDll, missingImportDll,
            appConfig,
            runtime6, runtime10,
            expiringCert,
            service, missingWorkerExe,
            batchA, batchB, batchC, sharedHostExe,
            healthyDll, healthyCert
        };

        var (result, context) = RiskPipeline.Run(entities);
        return (result, new Fixture(entities, service, missingWorkerExe, webDll, missingImportDll, expiringCert, appConfig, runtime6, runtime10, batchA, batchB, batchC, sharedHostExe, healthyDll, healthyCert));
    }

    private sealed record Fixture(
        List<DiscoveryEntity> Entities,
        Service Service,
        Dll MissingWorkerExe,
        Dll WebDll,
        Dll MissingImportDll,
        Certificate ExpiringCert,
        Configuration AppConfig,
        Runtime Runtime6,
        Runtime Runtime10,
        Service BatchA,
        Service BatchB,
        ScheduledTask BatchC,
        Dll SharedHostExe,
        Dll HealthyDll,
        Certificate HealthyCert);

    [Fact]
    public void Risk1_MissingErpWorkerExecutable_ProducesCriticalFinding()
    {
        var (result, fixture) = BuildAndAnalyze();

        var finding = Assert.Single(result.Findings, f => f.Metadata.GetValueOrDefault("MissingBinaryEntityId") == fixture.MissingWorkerExe.Id);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
        Assert.Contains(finding.Category, new[] { RiskCategory.MissingBinary, RiskCategory.Service });
    }

    [Fact]
    public void Risk2_MissingImportedNativeDependency_ProducesHighFinding()
    {
        var (result, fixture) = BuildAndAnalyze();

        var finding = Assert.Single(result.Findings, f => f.SourceEntityId == fixture.MissingImportDll.Id);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.Contains(finding.Category, new[] { RiskCategory.MissingBinary, RiskCategory.MissingDependency });
    }

    [Fact]
    public void Risk3_ExpiringHttpsCertificate_ProducesCertificateFinding()
    {
        var (result, fixture) = BuildAndAnalyze();

        var finding = Assert.Single(result.Findings, f => f.SourceEntityId == fixture.ExpiringCert.Id);
        Assert.Equal(RiskCategory.Certificate, finding.Category);
        Assert.Equal(RiskSeverity.High, finding.Severity); // 10 days remaining is within the High window
    }

    [Fact]
    public void Risk4_ExternalSqlServerDependency_ProducesMediumExternalDependencyFinding()
    {
        var (result, _) = BuildAndAnalyze();

        Assert.Contains(result.Findings, f =>
            f.Category == RiskCategory.ExternalDependency &&
            f.Severity == RiskSeverity.Medium &&
            f.Title.Contains("Database", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Risk5_ExternalFileShareDependency_ProducesHighExternalDependencyFinding()
    {
        var (result, _) = BuildAndAnalyze();

        Assert.Contains(result.Findings, f =>
            f.Category == RiskCategory.ExternalDependency &&
            f.Severity == RiskSeverity.High &&
            f.Title.Contains("FileShare", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Risk6_ExplicitNet8RequirementWithoutMatchingRuntime_ProducesMissingRuntimeFinding()
    {
        var (result, fixture) = BuildAndAnalyze();

        var finding = Assert.Single(result.Findings, f => f.SourceEntityId == fixture.AppConfig.Id && f.Category == RiskCategory.MissingRuntime);
        Assert.Contains("net8.0", finding.Title);
    }

    [Fact]
    public void Risk7_SharedWorkerBinaryAcrossThreeWorkloads_ProducesMediumSharedInfrastructureFinding()
    {
        var (result, fixture) = BuildAndAnalyze();

        var finding = Assert.Single(result.Findings, f => f.SourceEntityId == fixture.SharedHostExe.Id);
        Assert.Equal(RiskCategory.SharedInfrastructure, finding.Category);
        Assert.Equal(RiskSeverity.Medium, finding.Severity);
        Assert.Contains(fixture.BatchA.Id, finding.RelatedEntityIds);
        Assert.Contains(fixture.BatchB.Id, finding.RelatedEntityIds);
        Assert.Contains(fixture.BatchC.Id, finding.RelatedEntityIds);
    }

    [Fact]
    public void Risk8_PartiallyInaccessibleConfiguration_ProducesAccessDeniedFinding()
    {
        var (result, fixture) = BuildAndAnalyze();

        Assert.Contains(result.Findings, f => f.SourceEntityId == fixture.AppConfig.Id && f.Category == RiskCategory.AccessDenied);
    }

    [Fact]
    public void HealthyUnrelatedEntities_NeverProduceFindings()
    {
        var (result, fixture) = BuildAndAnalyze();

        Assert.DoesNotContain(result.Findings, f => f.SourceEntityId == fixture.HealthyDll.Id || fixture.HealthyDll.Id.Equals(f.SourceEntityId));
        Assert.DoesNotContain(result.Findings, f => f.RelatedEntityIds.Contains(fixture.HealthyDll.Id) && f.Category != RiskCategory.SharedInfrastructure);
        Assert.DoesNotContain(result.Findings, f => f.SourceEntityId == fixture.HealthyCert.Id);
    }

    [Fact]
    public void SiteApplicationPoolAndWebDll_NeverProduceFindings()
    {
        var (result, fixture) = BuildAndAnalyze();

        Assert.DoesNotContain(result.Findings, f => f.SourceEntityId == "iis-site:ERP" || f.SourceEntityId == "iis-application:ERP:/" || f.SourceEntityId == "iis-apppool:ERPAppPool");
        // The web DLL itself is healthy (found on disk) — only its missing import is a risk, keyed off the import target, not the importer.
        Assert.DoesNotContain(result.Findings, f => f.SourceEntityId == fixture.WebDll.Id);
    }
}
