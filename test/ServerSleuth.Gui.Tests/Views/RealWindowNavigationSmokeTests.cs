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

namespace ServerSleuth.Gui.Tests.Views;

/// <summary>A real bug slipped past every other test in this suite because every other test
/// exercises a ViewModel directly and never actually attaches a View to a live WPF layout pass
/// (see <c>App</c>'s static constructor doc comment and <see cref="RealWindowRuntimeConfigTests"/>
/// for the full story — that bug specifically required <c>InvariantGlobalization</c>'s real
/// runtime config, which this in-process test host does NOT itself have, so it alone cannot
/// reproduce or guard that one). This test still earns its place: it constructs the REAL
/// <see cref="GuiApp"/>/<see cref="MainWindow"/>/composition root (never a fake), runs a real
/// layout pass, and navigates through every page — catching any OTHER genuine WPF binding/
/// converter/layout exception a page might throw, regardless of runtime config. The closest thing
/// to interactive validation this automated suite can do without an actual interactive desktop
/// session.</summary>
public class RealWindowNavigationSmokeTests
{
    /// <summary>xUnit does not run test methods on an STA thread by default, and WPF windows
    /// require one — every test here marshals its real work onto a dedicated STA thread and
    /// surfaces any exception (or captured application error state) back to the calling thread.</summary>
    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw captured;
        }
    }

    private static void PumpDispatcherOnce()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary><see cref="Application"/> is a per-process singleton — it cannot be constructed
    /// more than once even across separate test methods, so both real-window scenarios run
    /// sequentially inside one STA session rather than as two independent <c>[Fact]</c>s.</summary>
    [Fact]
    public void RealWindow_NavigatingEveryPage_AndSwitchingLanguageThenNavigatingAgain_NeverProducesAnUnhandledException()
    {
        RunOnSta(() =>
        {
            var app = new GuiApp();
            app.InitializeComponent();

            Exception? dispatcherException = null;
            app.DispatcherUnhandledException += (_, e) =>
            {
                dispatcherException ??= e.Exception;
                e.Handled = true;
            };

            using (var services = (ServiceProvider)CompositionRoot.Build())
            {
                var mainViewModel = services.GetRequiredService<MainViewModel>();
                var window = new MainWindow(mainViewModel);

                window.Show();
                window.UpdateLayout();
                PumpDispatcherOnce();

                foreach (var page in Enum.GetValues<NavigationPage>())
                {
                    mainViewModel.NavigateCommand.Execute(page);
                    window.UpdateLayout();
                    PumpDispatcherOnce();
                }

                window.Close();

                var applicationState = services.GetRequiredService<IApplicationStateService>().Current;
                Assert.Null(applicationState.LastErrorMessage);
            }

            // The exact sequence a real user reported: Dashboard -> switch to Traditional
            // Chinese -> Scan — a fresh composition root/window, same live Application.
            using (var services = (ServiceProvider)CompositionRoot.Build())
            {
                var mainViewModel = services.GetRequiredService<MainViewModel>();
                var window = new MainWindow(mainViewModel);

                window.Show();
                window.UpdateLayout();
                PumpDispatcherOnce();

                mainViewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);
                window.UpdateLayout();
                PumpDispatcherOnce();

                foreach (var page in Enum.GetValues<NavigationPage>())
                {
                    mainViewModel.NavigateCommand.Execute(page);
                    window.UpdateLayout();
                    PumpDispatcherOnce();
                }

                window.Close();

                var applicationState = services.GetRequiredService<IApplicationStateService>().Current;
                Assert.Null(applicationState.LastErrorMessage);
            }

            Assert.Null(dispatcherException);
        });
    }
}
