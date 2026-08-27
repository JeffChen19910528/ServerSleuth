using System.Globalization;
using System.Windows;
using System.Windows.Markup;
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

    /// <summary>Works around a real, previously-undetected defect: with
    /// <c>InvariantGlobalization</c> enabled (see this project's own .csproj), WPF's data-binding
    /// engine still tries to resolve <see cref="FrameworkElement.Language"/>'s default value
    /// ("en-US") to a real, non-invariant <see cref="CultureInfo"/> the first time ANY bound
    /// control on ANY page actually attaches to the visual tree and runs a layout pass — which
    /// throws <c>InvalidOperationException: Cannot find non-neutral culture related to 'en-us'</c>,
    /// caught by this class's own <see cref="OnDispatcherUnhandledException"/> and surfaced only
    /// as the generic "unexpected error" message. GUI-1's Dashboard placeholder has no real bound
    /// controls, so this was never hit until a page with actual form bindings (Scan Configuration)
    /// was reached — reproduced with a real <see cref="Application"/>/<see cref="MainWindow"/>/
    /// real layout pass, unrelated to and reproducing with or without GUI-7's language toggle.
    /// The standard fix (not reversing the <c>InvariantGlobalization</c> choice, which this class
    /// has no documented reason to second-guess) is to override every <see cref="FrameworkElement"/>'s
    /// default <see cref="FrameworkElement.LanguageProperty"/> to the one culture that always
    /// exists under invariant globalization — before any <see cref="FrameworkElement"/> (including
    /// <see cref="MainWindow"/>) is ever constructed, hence the static constructor.</summary>
    static App()
    {
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.InvariantCulture.IetfLanguageTag)));
    }

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
