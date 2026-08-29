using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-7B's Settings page — deliberately NOT a new settings framework/subsystem: every property
/// below is a thin, direct proxy over state that already exists and is already the SINGLE
/// long-lived instance for the session — <see cref="ScanConfigurationViewModel"/> (a DI singleton,
/// see <c>CompositionRoot</c>) for scan defaults, and <see cref="ILanguageService"/> for language.
/// There is no separate storage anywhere in this type: reading e.g. <see cref="DefaultReportFormat"/>
/// reads <see cref="ScanConfigurationViewModel.OutputFormat"/> directly, and writing it writes the
/// SAME property — so a change here is immediately what Scan Configuration's own "Output Format"
/// selector already shows the next time the user opens it (skill.md's own "defaults for NEW scan
/// configuration, never silently alter an already-created ScanRequest" requirement: a `ScanRequest`
/// is an immutable record built once at "Start Scan" time, so nothing here can retroactively touch
/// one that already exists). No password/SecureString/credential-shaped property exists anywhere
/// on this type — <see cref="ScanConfigurationViewModel"/> itself structurally cannot expose its
/// private credential field as a bindable property (see that type's own <c>SetPassword</c> doc
/// comment), so there is nothing for this proxy to accidentally surface either.
/// </summary>
public sealed class SettingsViewModel : ObservableObject, IPageViewModel
{
    private readonly ScanConfigurationViewModel _scanConfiguration;
    private readonly ILanguageService _languageService;

    public SettingsViewModel(ScanConfigurationViewModel scanConfiguration, ILanguageService languageService)
    {
        _scanConfiguration = scanConfiguration;
        _languageService = languageService;

        SetLanguageCommand = new RelayCommand(parameter =>
        {
            if (parameter is GuiLanguage language)
            {
                _languageService.SetLanguage(language);
            }
        });
    }

    public NavigationPage Page => NavigationPage.Settings;

    // ----- General: all three proxy Scan Configuration's own singleton state directly — no
    // second copy, no "Apply"/"Save" step, and no effect on any ScanRequest already built. -----

    public string DefaultOutputDirectory
    {
        get => _scanConfiguration.OutputDirectory;
        set
        {
            if (_scanConfiguration.OutputDirectory != value)
            {
                _scanConfiguration.OutputDirectory = value;
                OnPropertyChanged();
            }
        }
    }

    public static IReadOnlyList<ScanOutputFormat> ReportFormatOptions { get; } = Enum.GetValues<ScanOutputFormat>();

    public ScanOutputFormat DefaultReportFormat
    {
        get => _scanConfiguration.OutputFormat;
        set
        {
            if (_scanConfiguration.OutputFormat != value)
            {
                _scanConfiguration.OutputFormat = value;
                OnPropertyChanged();
            }
        }
    }

    public static IReadOnlyList<ScanOverwritePolicy> OverwritePolicyOptions { get; } = Enum.GetValues<ScanOverwritePolicy>();

    public ScanOverwritePolicy DefaultOverwritePolicy
    {
        get => _scanConfiguration.OverwritePolicy;
        set
        {
            if (_scanConfiguration.OverwritePolicy != value)
            {
                _scanConfiguration.OverwritePolicy = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DefaultVerbose
    {
        get => _scanConfiguration.Verbose;
        set
        {
            if (_scanConfiguration.Verbose != value)
            {
                _scanConfiguration.Verbose = value;
                OnPropertyChanged();
            }
        }
    }

    // ----- Language — the SAME ILanguageService the header toggle already uses; offered here too
    // purely for discoverability, never a second language mechanism. -----

    public GuiLanguage CurrentLanguage => _languageService.CurrentLanguage;

    public RelayCommand SetLanguageCommand { get; }
}
