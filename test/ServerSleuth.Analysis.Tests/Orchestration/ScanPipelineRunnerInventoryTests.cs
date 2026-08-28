using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Analysis.Tests.Orchestration;

/// <summary>
/// GUI-6A: proves <see cref="ScanPipelineRunner.Analyze"/> carries the raw discovery snapshot
/// and the two Analysis-stage-computed-but-previously-discarded artifacts (application
/// boundaries, external dependencies) through to <see cref="ScanPipelineResult"/> WITHOUT
/// performing any new analysis — <see cref="ScanPipelineResult.Discovery"/> must be the exact
/// same <see cref="AggregateDiscoveryResult"/> instance passed in, and
/// <see cref="ScanPipelineResult.Boundaries"/>/<see cref="ScanPipelineResult.ExternalDependencies"/>
/// must reflect precisely what the existing <c>ApplicationBoundaryEngine</c>/
/// <c>DependencyExpansionEngine</c> stages inside <see cref="ScanPipelineRunner.Analyze"/> already
/// compute — never a re-derived or re-ordered value.
/// </summary>
public class ScanPipelineRunnerInventoryTests
{
    [Fact]
    public void Analyze_Result_CarriesTheExactSameDiscoveryInstance_PassedIn()
    {
        var discovery = BuildDiscovery();
        var runner = new ScanPipelineRunner(discoveryEngine: null!);

        var result = runner.Analyze(discovery, CancellationToken.None);

        Assert.Same(discovery, result.Discovery);
    }

    [Fact]
    public void Analyze_Result_Boundaries_ReflectsTheSameMembershipApplicationBoundaryEngineProduces()
    {
        var discovery = BuildDiscovery();
        var runner = new ScanPipelineRunner(discoveryEngine: null!);

        var result = runner.Analyze(discovery, CancellationToken.None);

        var boundary = Assert.Single(result.Boundaries);
        Assert.Contains("iis-application:ERP:/", boundary.MemberEntityIds);
        Assert.Contains(@"configuration:C:\ERP\Web\web.config", boundary.MemberEntityIds);
    }

    [Fact]
    public void Analyze_Result_ExternalDependencies_ReflectsWhatDependencyExpansionEngineExtracts()
    {
        var discovery = BuildDiscovery();
        var runner = new ScanPipelineRunner(discoveryEngine: null!);

        var result = runner.Analyze(discovery, CancellationToken.None);

        var externalDependency = Assert.Single(result.ExternalDependencies);
        Assert.Equal("ExternalDependency", externalDependency.Type);
        Assert.Contains("api.fixture.example.com", externalDependency.Name);
    }

    private static AggregateDiscoveryResult BuildDiscovery()
    {
        var site = EntityFactory.Site("ERP", @"C:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPPool");
        var app = EntityFactory.Application("ERP", "/", @"C:\ERP\Web", pool.Id, site.Id);

        var config = EntityFactory.Configuration(@"C:\ERP\Web\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Endpoint0.Scheme", "https");
        config.SetMetadata("Endpoint0.Host", "api.fixture.example.com");

        IReadOnlyList<DiscoveryEntity> entities = [site, pool, app, config];

        var scannerResult = DiscoveryResult.Success("fixture-scanner", entities);

        return new AggregateDiscoveryResult
        {
            Entities = entities,
            Errors = [],
            ScannerResults = [scannerResult],
            ScannerStatuses = new Dictionary<string, ScannerStatus> { ["fixture-scanner"] = ScannerStatus.Supported }
        };
    }
}
