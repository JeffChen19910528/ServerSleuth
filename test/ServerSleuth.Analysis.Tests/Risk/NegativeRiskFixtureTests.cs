using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

/// <summary>
/// Negative fixtures (skill.md Phase 7A §31): confirms the Risk Engine never produces a false
/// positive for situations that look superficially risky but are explicitly not migration
/// risks per each rule's own documented scope. Each test runs the FULL rule set (not a single
/// rule) so a false positive from any unexpected rule interaction would also be caught.
/// </summary>
public class NegativeRiskFixtureTests
{
    [Fact]
    public void ExpectedOrphanRuntime_InstalledButUnreferenced_NeverProducesFinding()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var entities = new List<DiscoveryEntity> { runtime };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ExpectedOrphanCertificate_UnreferencedAndFarFromExpiry_NeverProducesFinding()
    {
        var certificate = EntityFactory.Certificate("unused.example.com", "ORPHAN001", validTo: DateTimeOffset.UtcNow.AddYears(2));
        var entities = new List<DiscoveryEntity> { certificate };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void UnusedComRegistration_ServerResolvesOnDisk_NeverProducesFinding()
    {
        var com = EntityFactory.Com("{UNUSED-GUID}", inprocServer32: @"D:\Vendor\Vendor.dll");
        var dll = EntityFactory.Dll(@"D:\Vendor\Vendor.dll", notFound: false);
        var entities = new List<DiscoveryEntity> { com, dll };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void GenericInformationalOnlyExternalDependency_UnclassifiedKind_NeverProducesFinding()
    {
        var dependency = EntityFactory.ExternalDependency("Smtp", "smtp:relay.example.com", "relay.example.com");
        var entities = new List<DiscoveryEntity> { dependency };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void SharedParentDirectory_WithoutASharedExecutable_NeverProducesSharedInfrastructureFinding()
    {
        // Two Services under a common D:\Shared parent, but each running its OWN distinct
        // executable — common-parent-directory is recorded only as a weak, never-merged
        // ambiguous candidate (Phase 5B skill.md §9); it must never be treated as a shared
        // execution target by SharedInfrastructureRule.
        var serviceA = EntityFactory.Service("SvcA", @"D:\Shared\AppA\a.exe");
        var exeA = EntityFactory.Dll(@"D:\Shared\AppA\a.exe");
        var serviceB = EntityFactory.Service("SvcB", @"D:\Shared\AppB\b.exe");
        var exeB = EntityFactory.Dll(@"D:\Shared\AppB\b.exe");

        var entities = new List<DiscoveryEntity> { serviceA, exeA, serviceB, exeB };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void SameNamedDllsInUnrelatedApplicationRoots_NeverFalselyMergedAsSharedInfrastructure()
    {
        // Two same-named "worker.exe" binaries under entirely unrelated roots are distinct
        // entities (distinct path-based Ids) — never treated as one shared execution target.
        var serviceA = EntityFactory.Service("AlphaWorker", @"D:\Alpha\worker.exe");
        var alphaExe = EntityFactory.Dll(@"D:\Alpha\worker.exe");
        var serviceB = EntityFactory.Service("BetaWorker", @"E:\Beta\worker.exe");
        var betaExe = EntityFactory.Dll(@"E:\Beta\worker.exe");

        var entities = new List<DiscoveryEntity> { serviceA, alphaExe, serviceB, betaExe };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void FamilyOnlyRuntimeMarker_WithoutExplicitVersion_NeverProducesMissingRuntimeFinding()
    {
        var config = EntityFactory.Configuration(@"D:\ERP\web.config", dependencyReferences: ["Runtime: DotNet"]);
        var entities = new List<DiscoveryEntity> { config };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void AccessDeniedMetadata_OnAnEntityTypeTheRuleDoesNotConsider_NeverProducesFinding()
    {
        // AccessDeniedRule deliberately only inspects Configuration.ParseStatus and
        // Dll.FileStatus — an AccessDenied-flavored tag on an unrelated entity type (a
        // Certificate here) never identifies an actual migration-relevant dependency and must
        // never be surfaced as a risk.
        var certificate = EntityFactory.Certificate("noise.example.com", "NOISE001", validTo: DateTimeOffset.UtcNow.AddYears(1));
        certificate.SetMetadata("ParseStatus", "AccessDenied");
        var entities = new List<DiscoveryEntity> { certificate };

        var (result, _) = RiskPipeline.Run(entities);

        Assert.Empty(result.Findings);
    }
}
