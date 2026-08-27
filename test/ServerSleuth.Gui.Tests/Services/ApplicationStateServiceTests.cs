using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

/// <summary>GUI-1 §5, §12: the initial state is the deterministic, passive "nothing has
/// happened yet" snapshot, and every transition replaces the whole snapshot atomically.</summary>
public class ApplicationStateServiceTests
{
    [Fact]
    public void InitialState_IsPassive_NoTargetNotScanningNoResultsNoError()
    {
        var service = new ApplicationStateService();
        var state = service.Current;

        Assert.Equal(NavigationPage.Dashboard, state.CurrentPage);
        Assert.Null(state.Target);
        Assert.False(state.IsScanRunning);
        Assert.False(state.HasResults);
        Assert.Null(state.LastErrorMessage);
        Assert.False(state.IsExportAvailable);
    }

    [Fact]
    public void Update_ReplacesTheWholeSnapshot_PreviousSnapshotUnchanged()
    {
        var service = new ApplicationStateService();
        var before = service.Current;

        service.Update(s => s with { IsScanRunning = true });

        Assert.False(before.IsScanRunning); // the old record instance is untouched — immutability.
        Assert.True(service.Current.IsScanRunning);
    }

    [Fact]
    public void Update_RaisesStateChanged_WithTheNewSnapshot()
    {
        var service = new ApplicationStateService();
        GuiApplicationState? raised = null;
        service.StateChanged += (_, state) => raised = state;

        service.Update(s => s with { HasResults = true });

        Assert.NotNull(raised);
        Assert.True(raised!.HasResults);
    }

    [Fact]
    public void Update_CanSetATarget_WithNoCredentialField()
    {
        var service = new ApplicationStateService();
        var target = ScanTarget.Remote("db-server-1", TargetPlatform.Linux);

        service.Update(s => s with { Target = target });

        Assert.Equal(target, service.Current.Target);
    }

    [Fact]
    public void RepeatedIdenticalUpdates_AreDeterministic()
    {
        var serviceA = new ApplicationStateService();
        var serviceB = new ApplicationStateService();

        serviceA.Update(s => s with { IsScanRunning = true });
        serviceA.Update(s => s with { HasResults = true });
        serviceB.Update(s => s with { IsScanRunning = true });
        serviceB.Update(s => s with { HasResults = true });

        Assert.Equal(serviceA.Current, serviceB.Current);
    }
}
