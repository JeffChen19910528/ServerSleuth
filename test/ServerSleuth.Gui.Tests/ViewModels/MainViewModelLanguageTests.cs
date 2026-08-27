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

    [Fact]
    public void SetLanguageCommand_UpdatesThePlaceholderPage_WhenOneIsCurrentlyShown()
    {
        var viewModel = Build(out _);
        viewModel.NavigateCommand.Execute(NavigationPage.Migration);
        var before = (PlaceholderPageViewModel)viewModel.CurrentPageViewModel;
        Assert.Equal("Migration", before.Title);

        viewModel.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        var after = (PlaceholderPageViewModel)viewModel.CurrentPageViewModel;
        Assert.Equal("遷移", after.Title);
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
