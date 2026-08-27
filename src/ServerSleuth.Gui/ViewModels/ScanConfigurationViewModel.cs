using System.Collections.ObjectModel;
using System.Security;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-2's Scan Configuration ViewModel — the real ViewModel behind <see cref="NavigationPage.Scan"/>.
/// Owns the NON-sensitive <see cref="ScanConfigurationState"/> (freely bindable) and a
/// TRANSIENT, private <see cref="ScanCredentialInput"/> (never exposed as a bindable property —
/// see <see cref="SetPassword"/>'s own doc comment). Produces a <see cref="ScanRequest"/> via
/// <see cref="StartScanCommand"/> ONLY after validation succeeds — never executes anything: no
/// <c>DiscoveryEngine</c>/<c>ScanPipelineRunner</c>/scanner/transport is referenced anywhere in
/// this class (mechanically verified by <c>NoDirectPlatformAccessTests</c>/
/// <c>NoScanExecutionFromGuiTests</c>) — <see cref="ScanRequested"/> is the boundary a future
/// GUI-3 phase subscribes to instead.
/// </summary>
public sealed class ScanConfigurationViewModel : ObservableObject, IPageViewModel
{
    private readonly IScanConfigurationValidator _validator;
    private readonly IScanRequestFactory _requestFactory;
    private ScanCredentialInput _credentials = ScanCredentialInput.Empty;

    public ScanConfigurationViewModel(IScanConfigurationValidator validator, IScanRequestFactory requestFactory)
    {
        _validator = validator;
        _requestFactory = requestFactory;

        ValidateCommand = new RelayCommand(_ => Validate());
        StartScanCommand = new RelayCommand(_ => StartScan());
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    public NavigationPage Page => NavigationPage.Scan;

    /// <summary>Raised only when <see cref="StartScanCommand"/> runs against a VALID
    /// configuration — the GUI-3 execution boundary's own entry point
    /// (<c>MainViewModel</c> subscribes and hands both halves of the payload straight to
    /// <c>IGuiScanExecutor</c>). Never raised for an invalid configuration (skill.md GUI-2 §9).
    /// Widened from <c>EventHandler&lt;ScanRequest&gt;</c> to
    /// <c>EventHandler&lt;ScanRequestedEventArgs&gt;</c> in Phase GUI-3 so the transient
    /// credentials this ViewModel already held privately can be handed to the executor —
    /// <see cref="ScanRequest"/> itself remains credential-free, exactly as GUI-2 established.</summary>
    public event EventHandler<ScanRequestedEventArgs>? ScanRequested;

    // ----- Target -----

    private TargetKind _targetKind = TargetKind.Local;
    public TargetKind TargetKind
    {
        get => _targetKind;
        set
        {
            if (SetProperty(ref _targetKind, value))
            {
                if (value == TargetKind.Local)
                {
                    Platform = ScanConfigurationState.Initial.Platform;
                    TransportKind = null;
                }
                else
                {
                    ApplyDeterministicTransportForPlatform();
                }

                OnPropertyChanged(nameof(IsRemote));
                OnPropertyChanged(nameof(IsLocal));
            }
        }
    }

    public bool IsRemote => TargetKind == TargetKind.Remote;
    public bool IsLocal => TargetKind == TargetKind.Local;

    private TargetPlatform _platform = ScanConfigurationState.Initial.Platform;
    public TargetPlatform Platform
    {
        get => _platform;
        set
        {
            if (SetProperty(ref _platform, value) && IsRemote)
            {
                ApplyDeterministicTransportForPlatform();
            }

            OnPropertyChanged(nameof(IsLinuxRemote));
            OnPropertyChanged(nameof(IsWindowsRemote));
        }
    }

    public bool IsLinuxRemote => IsRemote && Platform == TargetPlatform.Linux;
    public bool IsWindowsRemote => IsRemote && Platform == TargetPlatform.Windows;

    private string _remoteHost = string.Empty;
    public string RemoteHost
    {
        get => _remoteHost;
        set => SetProperty(ref _remoteHost, value);
    }

    private int? _remotePort;
    public int? RemotePort
    {
        get => _remotePort;
        set => SetProperty(ref _remotePort, value);
    }

    /// <summary>skill.md GUI-2 §4: derived, deterministic, never independently choosable —
    /// there is structurally no way for the UI to select an unsupported (platform, transport)
    /// combination, since this is always recomputed from <see cref="Platform"/> alone.</summary>
    private ScanTransportKind? _transportKind;
    public ScanTransportKind? TransportKind
    {
        get => _transportKind;
        private set => SetProperty(ref _transportKind, value);
    }

    private void ApplyDeterministicTransportForPlatform() => TransportKind = Platform switch
    {
        TargetPlatform.Linux => ScanTransportKind.Ssh,
        TargetPlatform.Windows => ScanTransportKind.WinRm,
        _ => null
    };

    // ----- Non-sensitive remote connection metadata -----

    private string? _domain;
    public string? Domain
    {
        get => _domain;
        set => SetProperty(ref _domain, value);
    }

    private string? _sshPrivateKeyPath;
    public string? SshPrivateKeyPath
    {
        get => _sshPrivateKeyPath;
        set => SetProperty(ref _sshPrivateKeyPath, value);
    }

    private string? _sshPrivateKeyPassphraseEnvironmentVariable;
    public string? SshPrivateKeyPassphraseEnvironmentVariable
    {
        get => _sshPrivateKeyPassphraseEnvironmentVariable;
        set => SetProperty(ref _sshPrivateKeyPassphraseEnvironmentVariable, value);
    }

    private string? _sshHostFingerprint;
    public string? SshHostFingerprint
    {
        get => _sshHostFingerprint;
        set => SetProperty(ref _sshHostFingerprint, value);
    }

    private ScanWinRmAuthenticationMechanism _winRmAuthenticationMechanism = ScanWinRmAuthenticationMechanism.Negotiate;
    public ScanWinRmAuthenticationMechanism WinRmAuthenticationMechanism
    {
        get => _winRmAuthenticationMechanism;
        set => SetProperty(ref _winRmAuthenticationMechanism, value);
    }

    private bool _winRmUseSsl = true;
    public bool WinRmUseSsl
    {
        get => _winRmUseSsl;
        set => SetProperty(ref _winRmUseSsl, value);
    }

    // ----- Credentials (username is not secret; password never becomes a bound property) -----

    private string? _username;
    public string? Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    /// <summary>Called ONLY from <c>ScanConfigurationView</c>'s code-behind, reading
    /// <see cref="System.Windows.Controls.PasswordBox.SecurePassword"/> directly — never through
    /// a bound string property (skill.md GUI-2 §5, §8: "password must never enter
    /// GuiApplicationState... must never be displayed as plain text by default"). The
    /// <see cref="SecureString"/> is held only inside the private, non-bindable
    /// <see cref="_credentials"/> field for the lifetime of this ViewModel.</summary>
    public void SetPassword(SecureString? password) => _credentials = _credentials with { Password = password };

    // ----- Output -----

    private string _outputDirectory = string.Empty;
    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    private ScanOutputFormat _outputFormat = ScanOutputFormat.Both;
    public ScanOutputFormat OutputFormat
    {
        get => _outputFormat;
        set => SetProperty(ref _outputFormat, value);
    }

    private ScanOverwritePolicy _overwritePolicy = ScanOverwritePolicy.FailIfExists;
    public ScanOverwritePolicy OverwritePolicy
    {
        get => _overwritePolicy;
        set => SetProperty(ref _overwritePolicy, value);
    }

    private bool _verbose;
    public bool Verbose
    {
        get => _verbose;
        set => SetProperty(ref _verbose, value);
    }

    // ----- Validation -----

    public ObservableCollection<ScanConfigurationValidationError> ValidationErrors { get; } = [];

    private bool _isValid;
    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public RelayCommand ValidateCommand { get; }
    public RelayCommand StartScanCommand { get; }
    public RelayCommand CancelCommand { get; }

    public ScanConfigurationState BuildState() => new()
    {
        TargetKind = TargetKind,
        Platform = Platform,
        RemoteHost = RemoteHost,
        RemotePort = RemotePort,
        TransportKind = TransportKind,
        Domain = Domain,
        SshPrivateKeyPath = SshPrivateKeyPath,
        SshPrivateKeyPassphraseEnvironmentVariable = SshPrivateKeyPassphraseEnvironmentVariable,
        SshHostFingerprint = SshHostFingerprint,
        WinRmAuthenticationMechanism = WinRmAuthenticationMechanism,
        WinRmUseSsl = WinRmUseSsl,
        OutputDirectory = OutputDirectory,
        OutputFormat = OutputFormat,
        OverwritePolicy = OverwritePolicy,
        Verbose = Verbose
    };

    private ScanConfigurationValidationResult Validate()
    {
        var credentials = _credentials with { Username = Username };
        var result = _validator.Validate(BuildState(), credentials);

        ValidationErrors.Clear();
        foreach (var error in result.Errors)
        {
            ValidationErrors.Add(error);
        }

        IsValid = result.IsValid;
        return result;
    }

    /// <summary>skill.md GUI-2 §9-10 / GUI-3 §Step3: prepares the request and hands the
    /// credentials to whichever execution boundary is listening — never invokes discovery
    /// itself. The credentials captured here are the SAME ones <see cref="Validate"/> already
    /// validated against; after a successful hand-off this ViewModel drops its own reference to
    /// them (mirroring <see cref="Cancel"/>'s own clearing behavior) so the singleton
    /// configuration ViewModel never holds a credential any longer than the moment it takes to
    /// pass it on.</summary>
    private void StartScan()
    {
        var credentials = _credentials with { Username = Username };
        var result = Validate();
        if (!result.IsValid)
        {
            return;
        }

        var request = _requestFactory.Create(BuildState());
        ScanRequested?.Invoke(this, new ScanRequestedEventArgs { Request = request, Credentials = credentials });

        _credentials = ScanCredentialInput.Empty;
        Username = null;
    }

    /// <summary>skill.md GUI-2 §10: returns to a safe configuration state — clears validation
    /// state and any entered credential; never contacts a remote host (nothing here could:
    /// no transport type is referenced anywhere in this class).</summary>
    private void Cancel()
    {
        ValidationErrors.Clear();
        IsValid = false;
        _credentials = ScanCredentialInput.Empty;
        Username = null;
    }
}
