using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>
/// GUI-10 — proves <see cref="MigrationOverviewViewModel.PreparationSummary"/>/its per-intent
/// count properties are consumed from the shared <c>MigrationPreparationSummaryBuilder</c>
/// (relocated to <c>ServerSleuth.Analysis</c> by this phase precisely so the GUI can reuse it
/// without ever referencing <c>ServerSleuth.Reporting</c> — see
/// <c>ServerSleuth.Analysis.Migration.Preparation.MigrationIntent</c>'s own doc comment), never
/// recalculated, and is inventory-derived rather than Risk-derived.
/// </summary>
public class MigrationPreparationTests
{
    [Fact]
    public void PreparationSummary_IsAllZero_WhenNoPipelineResult()
    {
        var vm = new MigrationOverviewViewModel(ScanExecutionState.Idle);

        Assert.Equal(0, vm.PreparationSummary.TotalInventoryCount);
        Assert.False(vm.HasAnyPreparation);
        Assert.Equal(0, vm.DeployCount);
        Assert.Equal(0, vm.InstallCount);
        Assert.Equal(0, vm.CreateCount);
        Assert.Equal(0, vm.RegisterCount);
        Assert.Equal(0, vm.ConfigureCount);
        Assert.Equal(0, vm.VerifyCount);
        Assert.Equal(0, vm.ReviewCount);
    }

    [Fact]
    public void ServiceEntity_ContributesToCreateConfigureAndVerify()
    {
        var service = BuildService("Worker");
        var state = BuildState(applicationCount: 0, entities: [service], boundaries: []);

        var vm = new MigrationOverviewViewModel(state);

        Assert.True(vm.CreateCount >= 1);
        Assert.True(vm.ConfigureCount >= 1);
        Assert.True(vm.VerifyCount >= 1);
        Assert.Equal(0, vm.DeployCount);
        Assert.Equal(0, vm.RegisterCount);
        Assert.Equal(0, vm.InstallCount);
    }

    [Fact]
    public void DllEntity_ContributesToDeployAndVerify_NotCreate()
    {
        var dll = BuildDll("app.dll");
        var state = BuildState(applicationCount: 0, entities: [dll], boundaries: []);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(1, vm.DeployCount);
        Assert.True(vm.VerifyCount >= 1);
        Assert.Equal(0, vm.CreateCount);
    }

    [Fact]
    public void TotalInventoryCount_CountsServerWideUniqueEntities_NotPerApplicationSums()
    {
        // Same entity claimed by two boundaries: the per-application "Total*Count" properties
        // (GUI-8C) intentionally double it, but PreparationSummary's inventory count must not —
        // it counts the one, distinct, server-wide entity exactly once (skill.md GUI-10 §5, §11).
        var shared = BuildService("shared-svc");
        var state = BuildState(
            applicationCount: 2,
            entities: [shared],
            boundaries:
            [
                new ApplicationBoundary { Id = "boundary-00000", Name = "App 0", MemberEntityIds = [shared.Id], Confidence = Confidence.VeryHigh(), Reason = "test" },
                new ApplicationBoundary { Id = "boundary-00001", Name = "App 1", MemberEntityIds = [shared.Id], Confidence = Confidence.VeryHigh(), Reason = "test" }
            ]);

        var vm = new MigrationOverviewViewModel(state);

        // vm.TotalServiceCount (GUI-8C, per-application sum) double-counts the shared service —
        // correct for "how many components does each application need." The preparation
        // summary's TotalInventoryCount must not: one Service entity plus the two Application
        // boundaries themselves (each contributing to the "Application" category) — never the
        // shared Service counted twice.
        Assert.Equal(2, vm.TotalServiceCount);
        Assert.Equal(3, vm.PreparationSummary.TotalInventoryCount);
    }

    [Fact]
    public void PreparationSummary_IsUnaffectedByRiskFindingCount()
    {
        // Two fixtures with identical discovery entities but different FindingsPerApplication —
        // the preparation summary must be identical, proving it is inventory-derived, not
        // Risk-derived (skill.md GUI-9B §1, §7, §11; GUI-10 §4, §11, §12).
        var dll = BuildDll("app.dll");
        var stateNoFindings = ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 1,
            FindingsPerApplication = 0,
            DiscoveryEntities = [dll]
        });
        var stateWithFindings = ScanResultFixtureFactory.BuildCompletedState(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 1,
            FindingsPerApplication = 5,
            DiscoveryEntities = [dll]
        });

        var vmNoFindings = new MigrationOverviewViewModel(stateNoFindings);
        var vmWithFindings = new MigrationOverviewViewModel(stateWithFindings);

        Assert.Equal(vmNoFindings.DeployCount, vmWithFindings.DeployCount);
        Assert.Equal(vmNoFindings.VerifyCount, vmWithFindings.VerifyCount);
    }

    [Fact]
    public void ReviewCount_IsZero_UnderTheCurrentApprovedMapping()
    {
        var service = BuildService("Worker");
        var dll = BuildDll("app.dll");
        var state = BuildState(applicationCount: 0, entities: [service, dll], boundaries: []);

        var vm = new MigrationOverviewViewModel(state);

        Assert.Equal(0, vm.ReviewCount);
    }

    [Fact]
    public void PreparationSummary_IsDeterministic_AcrossRepeatedBuilds()
    {
        var service = BuildService("Worker");
        var state = BuildState(applicationCount: 0, entities: [service], boundaries: []);

        var vm1 = new MigrationOverviewViewModel(state);
        var vm2 = new MigrationOverviewViewModel(state);

        Assert.Equal(vm1.PreparationSummary.TotalInventoryCount, vm2.PreparationSummary.TotalInventoryCount);
        Assert.Equal(vm1.PreparationSummary.IntentCounts, vm2.PreparationSummary.IntentCounts);
    }

    [Theory]
    [InlineData("Migration.Preparation")]
    [InlineData("Migration.PreparationSummary")]
    [InlineData("Migration.Intent.Deploy")]
    [InlineData("Migration.Intent.Install")]
    [InlineData("Migration.Intent.Create")]
    [InlineData("Migration.Intent.Register")]
    [InlineData("Migration.Intent.Configure")]
    [InlineData("Migration.Intent.Verify")]
    [InlineData("Migration.Intent.Review")]
    public void LocalizedStrings_HasAllPreparationKeys_InBothLanguages(string key)
    {
        var en = ServerSleuth.Gui.Resources.LocalizedStrings.Get(key, ServerSleuth.Gui.Services.GuiLanguage.English);
        var zh = ServerSleuth.Gui.Resources.LocalizedStrings.Get(key, ServerSleuth.Gui.Services.GuiLanguage.TraditionalChinese);

        Assert.False(string.IsNullOrWhiteSpace(en));
        Assert.False(string.IsNullOrWhiteSpace(zh));
    }

    // ----- Helpers -----

    private static Service BuildService(string name) => new()
    {
        Id = $"service:{name}",
        Name = name,
        Type = "Service",
        Source = "ServiceControlManager",
        Status = EntityStatus.Running,
        Confidence = Confidence.VeryHigh()
    };

    private static Dll BuildDll(string name) => new()
    {
        Id = $"dll:{name}",
        Name = name,
        Type = "NativeDll",
        Source = "FileSystem",
        Status = EntityStatus.Referenced,
        Confidence = Confidence.High(),
        Path = $@"C:\apps\{name}"
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
