using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Gui.Composition;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.Views;

namespace ServerSleuth.Gui.App;

/// <summary>
/// The GUI's entry point. GUI-1 §12 (Security): startup here is PASSIVE — it builds the
/// composition root, constructs <see cref="MainWindow"/>, and shows it. Nothing in this class
/// (or anything it calls) starts a scan, contacts a remote host, enumerates the registry/
/// filesystem, or invokes SSH/WinRM — mechanically verified by
/// <c>CompositionDoesNotStartAScan_OrTouchAnyRemoteOrLocalResource</c>.
///
/// GUI-1 §8 (Error Boundary): every unhandled-exception surface WPF/.NET exposes
/// (<see cref="Dispatcher.UnhandledException"/> for the UI thread, <see cref="AppDomain.UnhandledException"/>
/// for anything WPF's own dispatcher didn't catch, <see cref="TaskScheduler.UnobservedTaskException"/>
/// for a faulted background <see cref="System.Threading.Tasks.Task"/> nobody awaited) is wired
/// here. Each one logs the FULL exception via the EXISTING <c>Microsoft.Extensions.Logging</c>
/// infrastructure (no second logging framework — skill.md GUI-1 §8's explicit instruction) and
/// publishes only a CONCISE, credential-free message into <see cref="IApplicationStateService"/>
/// — matching the exact "no stack trace in normal output" convention
/// <c>ServerSleuth.Cli.CliApplication</c> already established for the CLI's own top-level
/// exception handling.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = CompositionRoot.Build();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow(mainViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _services?.GetService<IGuiExceptionHandler>()?.Handle(e.Exception);
        e.Handled = true; // never let a UI-thread exception crash the process — surface it, don't hide it.
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _services?.GetService<IGuiExceptionHandler>()?.Handle(exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _services?.GetService<IGuiExceptionHandler>()?.Handle(e.Exception);
        e.SetObserved();
    }
}
