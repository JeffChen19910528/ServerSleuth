using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Gui.Composition;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.Composition;

/// <summary>
/// GUI-1 §Step11/§Step13: composition succeeds, resolves the shell ViewModel, and — the actual
/// performance requirement for this phase — completes near-instantly, since nothing it
/// registers constructs a scanner/discovery engine/remote transport of any kind (skill.md
/// GUI-1's own "DI composition should not execute discovery").
/// </summary>
public class CompositionRootTests
{
    [Fact]
    public void Build_Succeeds_AndResolvesMainViewModel()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build();
        var viewModel = provider.GetRequiredService<MainViewModel>();

        Assert.NotNull(viewModel);
        Assert.Equal(NavigationPage.Dashboard, viewModel.CurrentPageViewModel.Page);
    }

    [Fact]
    public void Build_RegistersOneSharedNavigationServiceAndStateService_NotOnePerResolution()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build();

        var navigationA = provider.GetRequiredService<INavigationService>();
        var navigationB = provider.GetRequiredService<INavigationService>();
        var stateA = provider.GetRequiredService<IApplicationStateService>();
        var stateB = provider.GetRequiredService<IApplicationStateService>();

        Assert.Same(navigationA, navigationB);
        Assert.Same(stateA, stateB);
    }

    [Fact]
    public void Build_CompletesQuickly_NoDiscoveryOrRemoteConnectionAttempted()
    {
        var stopwatch = Stopwatch.StartNew();
        using var provider = (ServiceProvider)CompositionRoot.Build();
        _ = provider.GetRequiredService<MainViewModel>();
        stopwatch.Stop();

        // A generous bound (real local/remote discovery takes seconds; this must take
        // milliseconds) — a regression here would indicate composition started doing real work.
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Composition took {stopwatch.ElapsedMilliseconds}ms — expected near-instant, no discovery/remote work.");
    }

    [Fact]
    public void Build_TheInitialApplicationState_IsPassive_NothingStartedAutomatically()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build();
        var state = provider.GetRequiredService<IApplicationStateService>().Current;

        Assert.False(state.IsScanRunning);
        Assert.Null(state.Target);
        Assert.False(state.HasResults);
    }

    /// <summary>GUI-3 §Step9, §Step16: resolving <c>IGuiScanExecutor</c> (and, transitively, the
    /// real <c>GuiScanExecutor</c> from <c>ServerSleuth.Gui.ExecutionHost</c>) merely constructs
    /// the object — its parameterless constructor only captures a reference to
    /// <c>DefaultGuiScanComposition.Build</c>, it never calls it. Resolution must remain as fast
    /// and passive as every other GUI-1 registration.</summary>
    [Fact]
    public void Build_ResolvesIGuiScanExecutor_WithoutStartingAnything()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build();
        var executor = provider.GetRequiredService<IGuiScanExecutor>();
        var executionViewModel = provider.GetRequiredService<ScanExecutionViewModel>();

        Assert.NotNull(executor);
        Assert.Equal(Models.ScanExecutionStatus.Idle, executionViewModel.State.Status);
    }
}
