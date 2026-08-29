using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.Tests.Fakes;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7: the language toggle, exercised through <see cref="MainViewModel"/> exactly
/// as the real <c>SetLanguageCommand</c> binding in <c>MainWindow.xaml</c> invokes it — a real
/// <see cref="LanguageService"/>, never a fake, since the whole point is proving the real
/// resolve-through-the-service path actually changes what's shown.</summary>
public class MainViewModelLanguageTests
{
    private static MainViewModel Build(out LanguageService language)
    {
        language = new LanguageService();
        var navigation = new NavigationService();
        var state = new ApplicationStateService();
        var scanConfiguration = new ScanConfigurationViewModel(new ScanConfigurationValidator(), new ScanRequestFactory());
        var scanExecution = new ScanExecutionViewModel(new FakeGuiScanExecutor());
        return new MainViewModel(navigation, state, scanConfiguration, scanExecution, languageService: language);
    }

    [Fact]
    public void DefaultLanguage_IsEnglish()
    {
        var viewModel = Build(out _);
        Assert.Equal(GuiLanguage.English, viewModel.CurrentLanguage);
    }

    [Fact]
    public void SetLanguageCommand_ToTraditionalChinese_ChangesCurrentLanguage()
    {
        var viewModel = Build(out _);

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        Assert.Equal(GuiLanguage.TraditionalChinese, viewModel.CurrentLanguage);
    }

    [Fact]
    public void SetLanguageCommand_ToTraditionalChinese_ChangesNavigationItemLabels()
    {
        var viewModel = Build(out _);
        var dashboardItem = viewModel.NavigationItems.Single(i => i.Page == NavigationPage.Dashboard);
        var englishLabel = dashboardItem.Label;

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        Assert.NotEqual(englishLabel, dashboardItem.Label);
        Assert.Equal("儀表板", dashboardItem.Label);
    }

    [Fact]
    public void SetLanguageCommand_BackToEnglish_RestoresTheOriginalLabel()
    {
        var viewModel = Build(out _);
        var dashboardItem = viewModel.NavigationItems.Single(i => i.Page == NavigationPage.Dashboard);
        var englishLabel = dashboardItem.Label;

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);
        viewModel.SetLanguageCommand.Execute(GuiLanguage.English);

        Assert.Equal(englishLabel, dashboardItem.Label);
    }

    [Fact]
    public void SetLanguageCommand_UpdatesTheStatusFooterText()
    {
        var viewModel = Build(out _);

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        Assert.Contains("尚未選擇掃描目標", viewModel.StatusText, StringComparison.Ordinal);
    }

    /// <summary>GUI-7B made Migration a real page (<see cref="MigrationOverviewViewModel"/>,
    /// no placeholder pages remain reachable at all) — this test's original premise (asserting a
    /// <c>PlaceholderPageViewModel.Title</c> re-resolved) no longer applies to ANY page, so it now
    /// verifies the equivalent, still-true behavior for a real page instead: a language switch
    /// while Migration is showing re-runs <c>ApplyCurrentPage</c> and still lands back on
    /// Migration (a fresh instance, matching every GUI-7A/GUI-7B page's own "rebuild fresh on
    /// every ApplyCurrentPage call" contract — see <c>DashboardOverviewViewModel</c>'s doc
    /// comment), never reverting to some other page or losing navigation state.</summary>
    [Fact]
    public void SetLanguageCommand_KeepsShowingTheSamePage_WhenARealGui7bPageIsCurrentlyShown()
    {
        var viewModel = Build(out _);
        viewModel.NavigateCommand.Execute(NavigationPage.Migration);
        var before = Assert.IsType<MigrationOverviewViewModel>(viewModel.CurrentPageViewModel);

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        var after = Assert.IsType<MigrationOverviewViewModel>(viewModel.CurrentPageViewModel);
        Assert.Equal(NavigationPage.Migration, after.Page);
        Assert.NotSame(before, after);
    }

    [Fact]
    public void SetLanguageCommand_DoesNotChangeCurrentPage_OrLoseScanConfigurationState()
    {
        var viewModel = Build(out _);
        viewModel.NavigateCommand.Execute(NavigationPage.Scan);
        var scanConfigBefore = viewModel.CurrentPageViewModel;

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        Assert.Same(scanConfigBefore, viewModel.CurrentPageViewModel);
        Assert.Equal(NavigationPage.Scan, viewModel.CurrentPageViewModel.Page);
    }
}
