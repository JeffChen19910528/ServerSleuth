using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-8C: aggregate inventory counts on <see cref="MigrationOverviewViewModel"/>.
/// Tests prove that TotalXxxCount properties sum correctly across all applications through the
/// boundary/entity join, never by re-running analysis.</summary>
public class MigrationChecklistTests
{
    // ----- Zero-state guards -----

    [Fact]
    public void AllTotalCounts_AreZero_WhenNoPipelineResult()
    {
        var vm = new MigrationOverviewViewModel(ScanExecutionState.Idle);

        Assert.Equal(0, vm.TotalDllBinaryCount);
        Assert.Equal(0, vm.TotalRuntimeCount);
        Assert.Equal(0, vm.TotalServiceCount);
        Assert.Equal(0, vm.TotalComComponentCount);
        Assert.Equal(0, vm.TotalSoftwareCount);
        Assert.Equal(0, vm.TotalScheduledTaskCount);
        Assert.Equal(0, vm.TotalCertificateCount);
        Assert.Equal(0, vm.TotalConfigurationCount);
        Assert.Equal(0, vm.TotalExternalConnectionCount);
        Assert.Equal(0, vm.TotalComponentCount);
    }

    [Fact]
    public void HasAnyComponents_IsFalse_WhenNoPipelineResult()
    {
        var vm = new MigrationOverviewViewModel(ScanExecutionState.Idle);
        Assert.False(vm.HasAnyComponents);
    }

    [Fact]
    public void AllTotalCounts_AreZero_WhenScanHasNoDiscoveryEntities()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState(
            new ScanResultFixtureFactory.Options { ApplicationCount = 2 });

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(0, vm.TotalComponentCount);
        Assert.False(vm.HasAnyComponents);
    }

    // ----- Per-type aggregation -----

    [Fact]
    public void TotalServiceCount_AggregatesAcrossAllApplicationBoundaries()
    {
        var service1 = BuildService("svc-1", "boundary-00000");
        var service2 = BuildService("svc-2", "boundary-00001");
        var state = BuildState(
            applicationCount: 2,
            entities: [service1, service2],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [service1.Id], Confidence = Confidence.VeryHigh(), Reason = "test" },
                new ApplicationBoundary { Id = "boundary-00001", Name = "App 1", MemberEntityIds = [service2.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(2, vm.TotalServiceCount);
    }

    [Fact]
    public void TotalDllBinaryCount_AggregatesAcrossAllApplicationBoundaries()
    {
        var dll1 = BuildDll("app.dll", "boundary-00000");
        var dll2 = BuildDll("lib.dll", "boundary-00000");
        var state = BuildState(
            applicationCount: 1,
            entities: [dll1, dll2],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [dll1.Id, dll2.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(2, vm.TotalDllBinaryCount);
    }

    [Fact]
    public void TotalComponentCount_IsTheSumOfAllNineTypeCounts()
    {
        var service = BuildService("svc", "boundary-00000");
        var dll = BuildDll("lib.dll", "boundary-00000");
        var cert = BuildCertificate("cert1", "boundary-00000");
        var state = BuildState(
            applicationCount: 1,
            entities: [service, dll, cert],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [service.Id, dll.Id, cert.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(
            vm.TotalDllBinaryCount + vm.TotalRuntimeCount + vm.TotalServiceCount + vm.TotalComComponentCount +
            vm.TotalSoftwareCount + vm.TotalScheduledTaskCount + vm.TotalCertificateCount +
            vm.TotalConfigurationCount + vm.TotalExternalConnectionCount,
            vm.TotalComponentCount);
        Assert.Equal(3, vm.TotalComponentCount);
    }

    [Fact]
    public void HasAnyComponents_IsTrue_WhenAtLeastOneComponentExists()
    {
        var service = BuildService("svc", "boundary-00000");
        var state = BuildState(
            applicationCount: 1,
            entities: [service],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [service.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        Assert.True(vm.HasAnyComponents);
    }

    // ----- Entity attribution rules -----

    [Fact]
    public void EntitiesWithNoBoundary_DoNotAppearInAnyCounts()
    {
        var unassigned = BuildService("unassigned-svc", "boundary-00000");
        // Boundary exists but memberships are empty
        var state = BuildState(
            applicationCount: 1,
            entities: [unassigned],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(0, vm.TotalServiceCount);
        Assert.Equal(0, vm.TotalComponentCount);
    }

    [Fact]
    public void EntityInMultipleBoundaries_IsCountedOncePerBoundaryItBelongsTo()
    {
        // Same entity referenced by two boundaries — expected behavior is double-count (one per app)
        var shared = BuildService("shared-svc", "boundary-00000");
        var state = BuildState(
            applicationCount: 2,
            entities: [shared],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [shared.Id], Confidence = Confidence.VeryHigh(), Reason = "test" },
                new ApplicationBoundary { Id = "boundary-00001", Name = "App 1", MemberEntityIds = [shared.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(2, vm.TotalServiceCount);
    }

    // ----- Determinism -----

    [Fact]
    public void AllCounts_AreDeterministic_AcrossMultipleViewModelBuilds()
    {
        var service = BuildService("svc", "boundary-00000");
        var dll = BuildDll("lib.dll", "boundary-00000");
        var state = BuildState(
            applicationCount: 1,
            entities: [service, dll],
            boundaries: [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [service.Id, dll.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm1 = new MigrationOverviewViewModel(state);
        var vm2 = new MigrationOverviewViewModel(state);

        Assert.Equal(vm1.TotalServiceCount, vm2.TotalServiceCount);
        Assert.Equal(vm1.TotalDllBinaryCount, vm2.TotalDllBinaryCount);
        Assert.Equal(vm1.TotalComponentCount, vm2.TotalComponentCount);
    }

    // ----- LocalizedStrings coverage -----

    [Theory]
    [InlineData("AppDetail.Action.Copy")]
    [InlineData("AppDetail.Action.Install")]
    [InlineData("AppDetail.Action.Create")]
    [InlineData("AppDetail.Action.Register")]
    [InlineData("AppDetail.Action.Configure")]
    [InlineData("AppDetail.Action.InstallSoftware")]
    [InlineData("AppDetail.Action.Verify")]
    public void LocalizedStrings_HasAllSevenActionKeys_InBothLanguages(string key)
    {
        var en = ServerSleuth.Gui.Resources.LocalizedStrings.Get(key, ServerSleuth.Gui.Services.GuiLanguage.English);
        var zh = ServerSleuth.Gui.Resources.LocalizedStrings.Get(key, ServerSleuth.Gui.Services.GuiLanguage.TraditionalChinese);

        Assert.False(string.IsNullOrWhiteSpace(en));
        Assert.False(string.IsNullOrWhiteSpace(zh));
    }

    [Fact]
    public void MigrationOverviewViewModel_HasNoCredentialShapedPublicProperties()
    {
        var forbiddenNames = new[] { "Credential", "SecureString", "Token", "Bearer", "PrivateKey", "Secret", "Authentication" };
        var type = typeof(MigrationOverviewViewModel);
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            foreach (var forbidden in forbiddenNames)
            {
                Assert.DoesNotContain(forbidden, prop.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ----- Helpers -----

    private static Service BuildService(string name, string boundaryHint) => new()
    {
        Id = $"service:{name}:{boundaryHint}",
        Name = name,
        Type = "Service",
        Source = "ServiceControlManager",
        Status = EntityStatus.Running,
        Confidence = Confidence.VeryHigh()
    };

    private static Dll BuildDll(string name, string boundaryHint) => new()
    {
        Id = $"dll:{name}:{boundaryHint}",
        Name = name,
        Type = "NativeDll",
        Source = "FileSystem",
        Status = EntityStatus.Referenced,
        Confidence = Confidence.High(),
        Path = $@"C:\apps\{name}"
    };

    private static Certificate BuildCertificate(string name, string boundaryHint) => new()
    {
        Id = $"cert:{name}:{boundaryHint}",
        Name = name,
        Type = "Certificate",
        Source = "WindowsCertificateStore",
        Status = EntityStatus.Installed,
        Confidence = Confidence.VeryHigh(),
        Thumbprint = name.ToUpperInvariant()
    };

    private static ScanExecutionState BuildState(
        int applicationCount,
        IReadOnlyList<DiscoveryEntity> entities,
        IReadOnlyList<ApplicationBoundary> boundaries)
    {
        return ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = applicationCount,
            FindingsPerApplication = 0,
            DiscoveryEntities = entities,
            Boundaries = boundaries
        });
    }
}
