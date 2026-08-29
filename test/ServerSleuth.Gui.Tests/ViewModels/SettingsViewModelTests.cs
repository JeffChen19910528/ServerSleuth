using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-7B: Settings is a thin proxy over <see cref="ScanConfigurationViewModel"/>'s own
/// already-existing properties and <see cref="ILanguageService"/> — deliberately no separate
/// storage anywhere (see <see cref="SettingsViewModel"/>'s own doc comment), so every test here
/// asserts the proxy actually reads/writes the SAME singleton instance, never a private copy.</summary>
public class SettingsViewModelTests
{
    private static (SettingsViewModel Settings, ScanConfigurationViewModel ScanConfiguration) Build()
    {
        var scanConfiguration = new ScanConfigurationViewModel(new ScanConfigurationValidator(), new ScanRequestFactory());
        var settings = new SettingsViewModel(scanConfiguration, new LanguageService());
        return (settings, scanConfiguration);
    }

    [Fact]
    public void Page_IsSettings()
    {
        var (settings, _) = Build();
        Assert.Equal(NavigationPage.Settings, settings.Page);
    }

    // ----- 22. Defaults are deterministic — mirror ScanConfigurationViewModel's own defaults. -----
    [Fact]
    public void Defaults_MatchScanConfigurationViewModelsOwnInitialValues()
    {
        var (settings, scanConfiguration) = Build();

        Assert.Equal(scanConfiguration.OutputDirectory, settings.DefaultOutputDirectory);
        Assert.Equal(scanConfiguration.OutputFormat, settings.DefaultReportFormat);
        Assert.Equal(scanConfiguration.OverwritePolicy, settings.DefaultOverwritePolicy);
        Assert.Equal(scanConfiguration.Verbose, settings.DefaultVerbose);
    }

    // ----- 23. Changing report format affects new scan defaults -----
    [Fact]
    public void ChangingDefaultReportFormat_ImmediatelyUpdatesScanConfigurationViewModelsOwnOutputFormat()
    {
        var (settings, scanConfiguration) = Build();

        settings.DefaultReportFormat = ScanOutputFormat.Json;

        Assert.Equal(ScanOutputFormat.Json, scanConfiguration.OutputFormat);
    }

    // ----- 24. Changing overwrite policy affects new scan defaults -----
    [Fact]
    public void ChangingDefaultOverwritePolicy_ImmediatelyUpdatesScanConfigurationViewModelsOwnOverwritePolicy()
    {
        var (settings, scanConfiguration) = Build();

        settings.DefaultOverwritePolicy = ScanOverwritePolicy.Overwrite;

        Assert.Equal(ScanOverwritePolicy.Overwrite, scanConfiguration.OverwritePolicy);
    }

    // ----- 25. Verbose preference is preserved -----
    [Fact]
    public void ChangingDefaultVerbose_ImmediatelyUpdatesScanConfigurationViewModelsOwnVerbose()
    {
        var (settings, scanConfiguration) = Build();

        settings.DefaultVerbose = true;

        Assert.True(scanConfiguration.Verbose);
        Assert.True(settings.DefaultVerbose);
    }

    [Fact]
    public void ChangingDefaultOutputDirectory_ImmediatelyUpdatesScanConfigurationViewModelsOwnOutputDirectory()
    {
        var (settings, scanConfiguration) = Build();

        settings.DefaultOutputDirectory = @"C:\scans\out";

        Assert.Equal(@"C:\scans\out", scanConfiguration.OutputDirectory);
    }

    /// <summary>skill.md GUI-7B §15: settings must never silently alter an already-created
    /// <c>ScanRequest</c> — that record is immutable once built, so this proves the point at the
    /// only place it could matter: a request built BEFORE a settings change keeps its own values.</summary>
    [Fact]
    public void ChangingASetting_NeverAltersAScanRequestAlreadyBuilt()
    {
        var (settings, scanConfiguration) = Build();
        scanConfiguration.OutputFormat = ScanOutputFormat.Html;
        var requestFactory = new ScanRequestFactory();
        var requestBefore = requestFactory.Create(scanConfiguration.BuildState());

        settings.DefaultReportFormat = ScanOutputFormat.Json;

        Assert.Equal(ScanOutputFormat.Html, requestBefore.OutputFormat);
    }

    // ----- Language: the same ILanguageService the header toggle already uses. -----
    [Fact]
    public void SetLanguageCommand_ChangesCurrentLanguage()
    {
        var (settings, _) = Build();

        settings.SetLanguageCommand.Execute(GuiLanguage.TraditionalChinese);

        Assert.Equal(GuiLanguage.TraditionalChinese, settings.CurrentLanguage);
    }

    // ----- 26./27. No password persistence / no credential-shaped settings — structural, see
    // NoCredentialShapedGuiStateTests (extended with this type: SettingsViewModel has no
    // property containing "password"/"credential"/etc., and structurally cannot reach
    // ScanConfigurationViewModel's private credential field). -----

    // ----- 28. No filesystem/network access merely by opening Settings — structural, see
    // NoScanExecutionFromGuiTests/NoDirectPlatformAccessTests (both extended/already covering
    // this type; its constructor takes only ScanConfigurationViewModel/ILanguageService). -----
}
