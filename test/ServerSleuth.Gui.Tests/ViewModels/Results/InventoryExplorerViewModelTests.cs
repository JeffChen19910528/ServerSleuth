using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>GUI-6A: the Discovery Inventory Explorer reads the raw entities an already-completed
/// scan discovered (never re-scans/re-analyzes), and never fabricates a category, an owner
/// application, or a "successful" state the underlying <see cref="ScanExecutionStatus"/> doesn't
/// support. Every test uses <see cref="ScanResultFixtureFactory"/> — hand-built data, never a
/// real pipeline run.</summary>
public class InventoryExplorerViewModelTests
{
    private static Service Service(string id, string name) => new()
    {
        Id = id, Name = name, Type = "Service", Source = "ServiceControlManager",
        Status = EntityStatus.Running, Confidence = Confidence.VeryHigh()
    };

    private static Certificate Certificate(string id, string name) => new()
    {
        Id = id, Name = name, Type = "Certificate", Source = "WindowsCertificateStore",
        Status = EntityStatus.Installed, Confidence = Confidence.VeryHigh(), Thumbprint = name
    };

    [Fact]
    public void EmptyDiscovery_HasNoInventory_AndNoFabricatedCategories()
    {
        var state = ScanResultFixtureFactory.BuildCompletedState();
        var vm = new ResultsDashboardViewModel(state);

        Assert.True(vm.Inventory.HasNoInventory);
        Assert.Empty(vm.Inventory.Items);
        Assert.Empty(vm.Inventory.Categories);
        Assert.Empty(vm.Inventory.FilteredItems);
    }

    [Fact]
    public void Categories_OnlyContainTypesActuallyDiscovered_WithRealCounts()
    {
        var options = new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 0,
            DiscoveryEntities = [Service("service:a", "Alpha"), Service("service:b", "Beta"), Certificate("cert:c", "Gamma")]
        };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);
        var vm = new ResultsDashboardViewModel(state);

        Assert.Equal(2, vm.Inventory.Categories.Count);
        Assert.Equal("Certificate", vm.Inventory.Categories[0].Type);
        Assert.Equal(1, vm.Inventory.Categories[0].Count);
        Assert.Equal("Service", vm.Inventory.Categories[1].Type);
        Assert.Equal(2, vm.Inventory.Categories[1].Count);
        Assert.Equal(3, vm.Inventory.TotalCount);
    }

    [Fact]
    public void Items_AreOrdered_TypeThenNameThenId_Deterministically()
    {
        var options = new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 0,
            DiscoveryEntities =
            [
                Service("service:z", "Zulu"), Service("service:a", "Alpha"), Certificate("cert:1", "OnlyCert")
            ]
        };
        var state = ScanResultFixtureFactory.BuildCompletedState(options);

        var first = new ResultsDashboardViewModel(state);
        var second = new ResultsDashboardViewModel(state);

        var firstOrder = first.Inventory.Items.Select(i => i.Id).ToList();
        var secondOrder = second.Inventory.Items.Select(i => i.Id).ToList();

        Assert.Equal(["cert:1", "service:a", "service:z"], firstOrder);
        Assert.Equal(firstOrder, secondOrder);
    }

    [Fact]
    public void SearchText_Filters_WithoutMutatingTheMasterItemsList()
    {
        var options = new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 0,
            DiscoveryEntities = [Service("service:a", "Alpha"), Service("service:b", "Beta")]
        };
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState(options));

        vm.Inventory.SearchText = "Alpha";

        Assert.Single(vm.Inventory.FilteredItems);
        Assert.Equal("Alpha", vm.Inventory.FilteredItems[0].Name);
        Assert.Equal(2, vm.Inventory.Items.Count);
    }

    [Fact]
    public void CategoryFilter_RestrictsToTheSelectedType_Only()
    {
        var options = new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 0,
            DiscoveryEntities = [Service("service:a", "Alpha"), Certificate("cert:1", "OnlyCert")]
        };
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState(options));

        vm.Inventory.SelectedCategory = "Certificate";

        Assert.Single(vm.Inventory.FilteredItems);
        Assert.Equal("Certificate", vm.Inventory.FilteredItems[0].Type);
    }

    [Fact]
    public void SelectingAnItem_ExposesItsDetail_WithEvidenceAndMetadataPassedThroughUnmodified()
    {
        var entity = Service("service:a", "Alpha");
        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ServiceConfiguration, Location = "SCM", Detail = "fixture" });
        entity.SetMetadata("StartType", "Automatic");

        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [entity] };
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState(options));

        vm.Inventory.SelectedItem = vm.Inventory.Items[0];

        Assert.NotNull(vm.Inventory.SelectedDetail);
        Assert.Single(vm.Inventory.SelectedDetail!.Evidence);
        Assert.Equal("Automatic", vm.Inventory.SelectedDetail.Metadata["StartType"]);
    }

    [Fact]
    public void EntityWithNoBoundaryMembership_ShowsAsUnassigned_NeverGuessed()
    {
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [Service("service:a", "Alpha")] };
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState(options));

        Assert.Equal("Unassigned", vm.Inventory.Items[0].ApplicationDisplay);
        Assert.False(vm.Inventory.Items[0].Detail.HasApplicationAttribution);
    }

    [Fact]
    public void SharedEntity_AcrossMultipleBoundaries_ListsEveryAffectedApplication_NeverOnlyTheFirst()
    {
        var shared = Service("service:shared", "SharedRuntime");
        var boundaries = new List<ApplicationBoundary>
        {
            new() { Id = "b1", Name = "BatchA", MemberEntityIds = ["service:shared"], Confidence = Confidence.High(), Reason = "fixture" },
            new() { Id = "b2", Name = "BatchB", MemberEntityIds = ["service:shared"], Confidence = Confidence.High(), Reason = "fixture" },
            new() { Id = "b3", Name = "BatchC", MemberEntityIds = ["service:shared"], Confidence = Confidence.High(), Reason = "fixture" }
        };

        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [shared], Boundaries = boundaries };
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState(options));

        var item = vm.Inventory.Items[0];
        Assert.Equal(["BatchA", "BatchB", "BatchC"], item.Detail.AffectedApplications);
        Assert.True(item.Detail.IsSharedAcrossApplications);
        Assert.Equal("BatchA, BatchB, BatchC", item.ApplicationDisplay);
    }

    [Fact]
    public void ExternalDependencies_AppearAsTheirOwnInventoryCategory()
    {
        var externalDependency = new ExternalDependency
        {
            Id = "external:api", Name = "api.example.com", Type = "ExternalDependency", Source = "Configuration",
            Status = EntityStatus.Referenced, Confidence = Confidence.Medium(), Kind = "HttpApi"
        };

        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, ExternalDependencies = [externalDependency] };
        var vm = new ResultsDashboardViewModel(ScanResultFixtureFactory.BuildCompletedState(options));

        Assert.Single(vm.Inventory.Categories);
        Assert.Equal("ExternalDependency", vm.Inventory.Categories[0].Type);
        Assert.Single(vm.Inventory.Items);
    }

    [Fact]
    public void PartialScan_SurfacesPartialCoverage_WithoutDroppingOtherScannersEntities()
    {
        var options = new ScanResultFixtureFactory.Options { ApplicationCount = 0, DiscoveryEntities = [Service("service:a", "Alpha")] };
        var state = ScanResultFixtureFactory.BuildCompletedState(options, ScanExecutionStatus.Partial);
        var vm = new ResultsDashboardViewModel(state);

        Assert.True(vm.Inventory.HasPartialCoverage);
        Assert.Single(vm.Inventory.Items);
    }

    [Fact]
    public void CancelledScan_ShowsEmptyInventory_NeverAFabricatedResult()
    {
        var state = ServerSleuth.Core.Targets.ScanTarget.Local();
        var executionState = ScanExecutionState.StartingFor(state).WithCompletion(ScanCompletionState.Cancelled());
        var vm = new ResultsDashboardViewModel(executionState);

        Assert.True(vm.Inventory.HasNoInventory);
        Assert.False(vm.Inventory.HasPartialCoverage);
    }
}
