using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Gui.Composition;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.Views;
using GuiApp = ServerSleuth.Gui.App.App;

// Deliberately real: a real Application, a real MainWindow, a real composition root, a real
// layout pass, under this project's own InvariantGlobalization=true (matching the shipped
// ServerSleuth.Gui.exe exactly) — see the .csproj comment for why an in-process xUnit test cannot
// exercise this. Exit code 0 = every scenario below completed with no unhandled exception and no
// LastErrorMessage ever set; any other exit code means a regression, with details on stderr for
// RealWindowRuntimeConfigTests (in ServerSleuth.Gui.Tests) to surface in its assertion failure.

var failures = new List<string>();

var thread = new Thread(() => Run(failures));
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("OK — every page, with and without a language switch, produced no unhandled exception.");
return 0;

static void Run(List<string> failures)
{
    var app = new GuiApp();
    app.InitializeComponent();

    Exception? dispatcherException = null;
    app.DispatcherUnhandledException += (_, e) =>
    {
        dispatcherException ??= e.Exception;
        e.Handled = true;
    };

    // Not every exception this scenario can throw necessarily routes through
    // DispatcherUnhandledException (a synchronous call on this thread, e.g. UpdateLayout()
    // itself, can throw directly) — catch here too so the failure is reported cleanly either way.
    try
    {
        RunScenario(failures, () => dispatcherException, ex => dispatcherException = ex, switchToTraditionalChinese: false);
    }
    catch (Exception ex)
    {
        failures.Add($"[without language switch] Threw directly (not via Dispatcher): {ex}");
    }

    try
    {
        RunScenario(failures, () => dispatcherException, ex => dispatcherException = ex, switchToTraditionalChinese: true);
    }
    catch (Exception ex)
    {
        failures.Add($"[with language switch] Threw directly (not via Dispatcher): {ex}");
    }
}

static void RunScenario(List<string> failures, Func<Exception?> getDispatcherException, Action<Exception?> resetDispatcherException, bool switchToTraditionalChinese)
{
    resetDispatcherException(null);

    using var services = (ServiceProvider)CompositionRoot.Build();
    var mainViewModel = services.GetRequiredService<MainViewModel>();
    var window = new MainWindow(mainViewModel);

    window.Show();
    window.UpdateLayout();
    PumpDispatcherOnce();

    if (switchToTraditionalChinese)
    {
        mainViewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    foreach (var page in Enum.GetValues<NavigationPage>())
    {
        mainViewModel.NavigateCommand.Execute(page);
        window.UpdateLayout();
        PumpDispatcherOnce();
    }

    window.Close();

    var applicationState = services.GetRequiredService<IApplicationStateService>().Current;
    var scenario = switchToTraditionalChinese ? "with language switch" : "without language switch";

    if (getDispatcherException() is { } exception)
    {
        failures.Add($"[{scenario}] DispatcherUnhandledException: {exception}");
    }

    if (applicationState.LastErrorMessage is { } message)
    {
        failures.Add($"[{scenario}] LastErrorMessage was set: {message}");
    }
}

static void PumpDispatcherOnce()
{
    var frame = new DispatcherFrame();
    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
    Dispatcher.PushFrame(frame);
}
