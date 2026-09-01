using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>
/// GUI-8A: per-type inventory counts on <see cref="DashboardOverviewViewModel"/>. Every count
/// comes from <c>ScanPipelineResult.Discovery.Entities</c> (counted by C# class, not Type
/// string) or from <c>ScanPipelineResult.ExternalDependencies</c> for External Connections.
/// No scanner is run; all entities are hand-built fixtures so tests never reference
/// Windows/Linux/Infrastructure. Counts never mutate after construction (read-only properties
/// set in the constructor), so two ViewModels built from the same state produce identical counts.
/// </summary>
public class DashboardInventoryCountsTests
{
    // ----- 1. Empty / no-scan state -----

    [Fact]
    public void NoScanYet_AllInventoryCountsAreZero()
    {
        var vm = new DashboardOverviewViewModel(ScanExecutionState.Idle);

        Assert.Equal(0, vm.ApplicationEntityCount);
        Assert.Equal(0, vm.DllEntityCount);
        Assert.Equal(0, vm.ServiceEntityCount);
        Assert.Equal(0, vm.ComComponentEntityCount);
        Assert.Equal(0, vm.SoftwareEntityCount);
        Assert.Equal(0, vm.RuntimeEntityCount);
        Assert.Equal(0, vm.ScheduledTaskEntityCount);
        Assert.Equal(0, vm.CertificateEntityCount);
        Assert.Equal(0, vm.ConfigurationEntityCount);
        Assert.Equal(0, vm.ExternalConnectionCount);
    }

    // ----- 2. Individual type counts -----

    [Fact]
    public void Applications_CountsApplicationEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeApp("app-1"),
            MakeApp("app-2"),
            MakeService("svc-1"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(2, vm.ApplicationEntityCount);
        Assert.Equal(1, vm.ServiceEntityCount);
    }

    [Fact]
    public void DllBinaries_CountsDllEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeDll("dll-1", "NativeBinary"),
            MakeDll("dll-2", "NativeBinary"),
            MakeDll("dll-3", "Dll"),
            MakeService("svc-1"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(3, vm.DllEntityCount);
        Assert.Equal(1, vm.ServiceEntityCount);
    }

    [Fact]
    public void Services_CountsServiceEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeService("svc-1"),
            MakeService("svc-2"),
            MakeService("svc-3"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(3, vm.ServiceEntityCount);
    }

    [Fact]
    public void ComComponents_CountsComComponentEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeCom("com-1"),
            MakeCom("com-2"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(2, vm.ComComponentEntityCount);
    }

    [Fact]
    public void Software_CountsSoftwareEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeSoftware("sw-1"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(1, vm.SoftwareEntityCount);
    }

    [Fact]
    public void Runtime_CountsRuntimeEntities()
    {
        // Runtime entities use Type = family name (e.g. "DotNetRuntime"), not "Runtime" —
        // the count is by C# class, not Type string, so any family is counted here.
        var entities = new DiscoveryEntity[]
        {
            MakeRuntime("rt-1", "DotNetRuntime"),
            MakeRuntime("rt-2", "DotNetSdk"),
            MakeRuntime("rt-3", "Java"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(3, vm.RuntimeEntityCount);
    }

    [Fact]
    public void ScheduledTasks_CountsScheduledTaskEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeScheduledTask("task-1"),
            MakeScheduledTask("task-2"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(2, vm.ScheduledTaskEntityCount);
    }

    [Fact]
    public void Certificates_CountsCertificateEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeCertificate("cert-1"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(1, vm.CertificateEntityCount);
    }

    [Fact]
    public void Configuration_CountsConfigurationEntities()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeConfiguration("cfg-1"),
            MakeConfiguration("cfg-2"),
            MakeConfiguration("cfg-3"),
        };
        var vm = BuildWithEntities(entities);

        Assert.Equal(3, vm.ConfigurationEntityCount);
    }

    [Fact]
    public void ExternalConnections_CountsFromExternalDependencies()
    {
        var extDeps = new ExternalDependency[]
        {
            MakeExternal("ext-1"),
            MakeExternal("ext-2"),
        };
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ExternalDependencies = extDeps });
        var vm = new DashboardOverviewViewModel(state);

        Assert.Equal(2, vm.ExternalConnectionCount);
    }

    // ----- 3. Mixed-entity scenario: all 10 categories counted independently -----

    [Fact]
    public void MixedEntities_EachCategoryCountsCorrectly()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeApp("app-1"),
            MakeApp("app-2"),
            MakeDll("dll-1", "NativeBinary"),
            MakeService("svc-1"),
            MakeService("svc-2"),
            MakeService("svc-3"),
            MakeCom("com-1"),
            MakeSoftware("sw-1"),
            MakeSoftware("sw-2"),
            MakeRuntime("rt-1", "DotNetRuntime"),
            MakeScheduledTask("task-1"),
            MakeCertificate("cert-1"),
            MakeCertificate("cert-2"),
            MakeConfiguration("cfg-1"),
        };
        var extDeps = new ExternalDependency[] { MakeExternal("ext-1"), MakeExternal("ext-2"), MakeExternal("ext-3") };
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options
            {
                DiscoveryEntities = entities,
                ExternalDependencies = extDeps
            });
        var vm = new DashboardOverviewViewModel(state);

        Assert.Equal(2, vm.ApplicationEntityCount);
        Assert.Equal(1, vm.DllEntityCount);
        Assert.Equal(3, vm.ServiceEntityCount);
        Assert.Equal(1, vm.ComComponentEntityCount);
        Assert.Equal(2, vm.SoftwareEntityCount);
        Assert.Equal(1, vm.RuntimeEntityCount);
        Assert.Equal(1, vm.ScheduledTaskEntityCount);
        Assert.Equal(2, vm.CertificateEntityCount);
        Assert.Equal(1, vm.ConfigurationEntityCount);
        Assert.Equal(3, vm.ExternalConnectionCount);
    }

    // ----- 4. Deterministic: same state → same counts -----

    [Fact]
    public void BuildingTwiceFromTheSameState_ProducesIdenticalInventoryCounts()
    {
        var entities = new DiscoveryEntity[]
        {
            MakeApp("app-1"),
            MakeService("svc-1"),
            MakeCertificate("cert-1"),
        };
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { DiscoveryEntities = entities });

        var first = new DashboardOverviewViewModel(state);
        var second = new DashboardOverviewViewModel(state);

        Assert.Equal(first.ApplicationEntityCount, second.ApplicationEntityCount);
        Assert.Equal(first.DllEntityCount, second.DllEntityCount);
        Assert.Equal(first.ServiceEntityCount, second.ServiceEntityCount);
        Assert.Equal(first.ComComponentEntityCount, second.ComComponentEntityCount);
        Assert.Equal(first.SoftwareEntityCount, second.SoftwareEntityCount);
        Assert.Equal(first.RuntimeEntityCount, second.RuntimeEntityCount);
        Assert.Equal(first.ScheduledTaskEntityCount, second.ScheduledTaskEntityCount);
        Assert.Equal(first.CertificateEntityCount, second.CertificateEntityCount);
        Assert.Equal(first.ConfigurationEntityCount, second.ConfigurationEntityCount);
        Assert.Equal(first.ExternalConnectionCount, second.ExternalConnectionCount);
    }

    // ----- 5. Empty Discovery.Entities list (scan completed but nothing discovered) -----

    [Fact]
    public void CompletedScanWithNoEntities_AllCountsAreZero()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options
            {
                DiscoveryEntities = [],
                ExternalDependencies = []
            });
        var vm = new DashboardOverviewViewModel(state);

        Assert.True(vm.HasResults);
        Assert.Equal(0, vm.ApplicationEntityCount);
        Assert.Equal(0, vm.DllEntityCount);
        Assert.Equal(0, vm.ServiceEntityCount);
        Assert.Equal(0, vm.ComComponentEntityCount);
        Assert.Equal(0, vm.SoftwareEntityCount);
        Assert.Equal(0, vm.RuntimeEntityCount);
        Assert.Equal(0, vm.ScheduledTaskEntityCount);
        Assert.Equal(0, vm.CertificateEntityCount);
        Assert.Equal(0, vm.ConfigurationEntityCount);
        Assert.Equal(0, vm.ExternalConnectionCount);
    }

    // ----- 6. Counts are independent of existing Risk/Migration summary numbers -----

    [Fact]
    public void InventoryCounts_AreIndependentOfRiskAndMigrationSummaries()
    {
        var entities = new DiscoveryEntity[] { MakeService("svc-1") };
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options
            {
                ApplicationCount = 5,
                FindingsPerApplication = 3,
                DiscoveryEntities = entities
            });
        var vm = new DashboardOverviewViewModel(state);

        // Risk and migration counts come from the report summaries (pre-computed by Analysis).
        Assert.True(vm.CriticalCount + vm.HighCount + vm.MediumCount >= 0);
        Assert.True(vm.ApplicationCount == 5);

        // Inventory counts come directly from Discovery.Entities — unaffected by findings count.
        Assert.Equal(1, vm.ServiceEntityCount);
        Assert.Equal(0, vm.ApplicationEntityCount);
    }

    // ----- Helpers -----

    private static DashboardOverviewViewModel BuildWithEntities(DiscoveryEntity[] entities)
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { DiscoveryEntities = entities });
        return new DashboardOverviewViewModel(state);
    }

    private static Application MakeApp(string id) =>
        new() { Id = id, Name = id, Type = "Application", Source = "IIS" };

    private static Dll MakeDll(string id, string type) =>
        new() { Id = id, Name = id, Type = type, Source = "FileSystem" };

    private static Service MakeService(string id) =>
        new() { Id = id, Name = id, Type = "Service", Source = "ServiceControlManager" };

    private static ComComponent MakeCom(string id) =>
        new() { Id = id, Name = id, Type = "ComComponent", Source = "Registry", Clsid = $"{{{id}}}" };

    private static Software MakeSoftware(string id) =>
        new() { Id = id, Name = id, Type = "Software", Source = "Registry" };

    private static Runtime MakeRuntime(string id, string family) =>
        new() { Id = id, Name = id, Type = family, Source = "Command" };

    private static ScheduledTask MakeScheduledTask(string id) =>
        new() { Id = id, Name = id, Type = "ScheduledTask", Source = "TaskScheduler" };

    private static Certificate MakeCertificate(string id) =>
        new() { Id = id, Name = id, Type = "Certificate", Source = "CertStore" };

    private static Configuration MakeConfiguration(string id) =>
        new() { Id = id, Name = id, Type = "Configuration", Source = "FileSystem" };

    private static ExternalDependency MakeExternal(string id) =>
        new() { Id = id, Name = id, Type = "ExternalDependency", Source = "Configuration" };
}
