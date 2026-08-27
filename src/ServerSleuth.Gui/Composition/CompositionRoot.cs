using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Gui.ExecutionHost;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Composition;

/// <summary>
/// The GUI's own composition root — see skill.md GUI-1 §Step13/§Performance: "DI composition
/// should not execute discovery." <see cref="Build"/> registers ONLY GUI-shell services
/// (navigation, application state, the main shell ViewModel) — nothing here constructs a
/// <c>DiscoveryEngine</c>/scanner registry/remote transport of any kind, so building this
/// container is always instantaneous and side-effect-free, mechanically verified by
/// <c>CompositionDoesNotStartAScan_OrTouchAnyRemoteOrLocalResource</c>.
/// </summary>
public static class CompositionRoot
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddSingleton<IApplicationStateService, ApplicationStateService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IGuiExceptionHandler, GuiExceptionHandler>();
        services.AddSingleton<IScanConfigurationValidator, ScanConfigurationValidator>();
        services.AddSingleton<IScanRequestFactory, ScanRequestFactory>();
        services.AddSingleton<ScanConfigurationViewModel>();
        services.AddSingleton<IGuiScanExecutor, GuiScanExecutor>();
        services.AddSingleton<ScanExecutionViewModel>();

        // GUI-5 §1-2: the same composition/execution-boundary pattern as IGuiScanExecutor above
        // (real implementation in ServerSleuth.Gui.ExecutionHost) plus the local, Reporting-free
        // report-file reader (see IGuiReportViewerService's own doc comment for why that one does
        // NOT need the ExecutionHost boundary).
        services.AddSingleton<IGuiReportExportService, GuiReportExportService>();
        services.AddSingleton<IGuiReportViewerService, GuiReportViewerService>();

        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
