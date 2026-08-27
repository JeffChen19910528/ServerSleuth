using System.Diagnostics;
using System.Security;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;
using ServerSleuth.Gui.ViewModels;

namespace ServerSleuth.Gui.Tests.ViewModels;

/// <summary>GUI-2 §Step12: the Scan Configuration ViewModel's behavior — target/transport
/// selection, credential handling, validation, and the Start Scan boundary. Every test uses the
/// REAL <see cref="ScanConfigurationValidator"/>/<see cref="ScanRequestFactory"/> — both are
/// pure, deterministic, side-effect-free classes, so no fake is needed.</summary>
public class ScanConfigurationViewModelTests
{
    private static SecureString Secure(string value)
    {
        var secure = new SecureString();
        foreach (var ch in value)
        {
            secure.AppendChar(ch);
        }

        secure.MakeReadOnly();
        return secure;
    }

    private static ScanConfigurationViewModel Build() =>
        new(new ScanConfigurationValidator(), new ScanRequestFactory());

    // 1. Default configuration.
    [Fact]
    public void DefaultConfiguration_IsLocal_WithTheResolvedLocalPlatform()
    {
        var viewModel = Build();

        Assert.Equal(TargetKind.Local, viewModel.TargetKind);
        Assert.True(viewModel.IsLocal);
        Assert.False(viewModel.IsRemote);
        Assert.Null(viewModel.TransportKind);
        Assert.Equal(NavigationPageScan(), viewModel.Page);
    }

    private static ServerSleuth.Gui.Navigation.NavigationPage NavigationPageScan() => ServerSleuth.Gui.Navigation.NavigationPage.Scan;

    // 2. Local target.
    [Fact]
    public void LocalTarget_DoesNotRequireHostOrCredentials_ToValidate()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = "./out";

        viewModel.ValidateCommand.Execute(null);

        Assert.True(viewModel.IsValid);
        Assert.Empty(viewModel.ValidationErrors);
    }

    // 3. Remote Linux.
    [Fact]
    public void SwitchingToRemoteLinux_DerivesSshTransport()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Linux;

        Assert.True(viewModel.IsLinuxRemote);
        Assert.Equal(ScanTransportKind.Ssh, viewModel.TransportKind);
    }

    // 4. Remote Windows.
    [Fact]
    public void SwitchingToRemoteWindows_DerivesWinRmTransport()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Windows;

        Assert.True(viewModel.IsWindowsRemote);
        Assert.Equal(ScanTransportKind.WinRm, viewModel.TransportKind);
    }

    // 5. Switching Local → Remote.
    [Fact]
    public void SwitchingLocalToRemote_EnablesRemoteFields()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;

        Assert.True(viewModel.IsRemote);
        Assert.False(viewModel.IsLocal);
    }

    // 6. Switching Remote → Local.
    [Fact]
    public void SwitchingRemoteToLocal_ClearsTransportKind_AndResetsPlatformToLocalDefault()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Windows;

        viewModel.TargetKind = TargetKind.Local;

        Assert.Null(viewModel.TransportKind);
        Assert.Equal(ScanConfigurationState.Initial.Platform, viewModel.Platform);
    }

    // 7. Linux → SSH selection (structurally derived, never independently choosable).
    [Fact]
    public void LinuxPlatform_AlwaysMapsToSsh_NeverAnyOtherTransport()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;

        viewModel.Platform = TargetPlatform.Windows; // start from the other platform
        viewModel.Platform = TargetPlatform.Linux;

        Assert.Equal(ScanTransportKind.Ssh, viewModel.TransportKind);
    }

    // 8. Windows → WinRM selection.
    [Fact]
    public void WindowsPlatform_AlwaysMapsToWinRm_NeverAnyOtherTransport()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;

        viewModel.Platform = TargetPlatform.Linux;
        viewModel.Platform = TargetPlatform.Windows;

        Assert.Equal(ScanTransportKind.WinRm, viewModel.TransportKind);
    }

    // 9-10. An invalid (platform, transport) combination is structurally unreachable — TransportKind
    // has no public setter; it is ALWAYS derived from Platform. These tests lock that in.
    [Fact]
    public void TransportKind_HasNoPublicSetter_InvalidCombinationsAreStructurallyImpossible()
    {
        var property = typeof(ScanConfigurationViewModel).GetProperty(nameof(ScanConfigurationViewModel.TransportKind));
        Assert.NotNull(property);
        Assert.False(property!.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void EveryPlatformValue_ProducesOnlyItsOwnSupportedTransport_ForAllTransitionOrders()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;

        foreach (var platform in new[] { TargetPlatform.Windows, TargetPlatform.Linux, TargetPlatform.Windows, TargetPlatform.Linux })
        {
            viewModel.Platform = platform;
            var expected = platform == TargetPlatform.Linux ? ScanTransportKind.Ssh : ScanTransportKind.WinRm;
            Assert.Equal(expected, viewModel.TransportKind);
        }
    }

    // 11. Empty remote hostname.
    [Fact]
    public void RemoteWithEmptyHostname_FailsValidation()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Linux;
        viewModel.SshPrivateKeyPath = "/key";
        viewModel.SshHostFingerprint = "aa:bb";
        viewModel.Username = "tester";
        viewModel.OutputDirectory = "./out";

        viewModel.ValidateCommand.Execute(null);

        Assert.False(viewModel.IsValid);
        Assert.Contains(viewModel.ValidationErrors, e => e.Field == nameof(ScanConfigurationState.RemoteHost));
    }

    // 12. Valid remote hostname.
    [Fact]
    public void RemoteWithValidHostname_DoesNotFailOnHostname()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Linux;
        viewModel.RemoteHost = "db-server-1";
        viewModel.SshPrivateKeyPath = "/key";
        viewModel.SshHostFingerprint = "aa:bb";
        viewModel.Username = "tester";
        viewModel.OutputDirectory = "./out";

        viewModel.ValidateCommand.Execute(null);

        Assert.DoesNotContain(viewModel.ValidationErrors, e => e.Field == nameof(ScanConfigurationState.RemoteHost));
    }

    // 13-14. Output format.
    [Theory]
    [InlineData(ScanOutputFormat.Json)]
    [InlineData(ScanOutputFormat.Html)]
    public void OutputFormat_IsBoundAndPreserved(ScanOutputFormat format)
    {
        var viewModel = Build();
        viewModel.OutputFormat = format;
        Assert.Equal(format, viewModel.OutputFormat);
        Assert.Equal(format, viewModel.BuildState().OutputFormat);
    }

    // 15. Overwrite toggle.
    [Fact]
    public void OverwritePolicy_TogglesBetweenFailIfExistsAndOverwrite()
    {
        var viewModel = Build();
        Assert.Equal(ScanOverwritePolicy.FailIfExists, viewModel.OverwritePolicy);

        viewModel.OverwritePolicy = ScanOverwritePolicy.Overwrite;

        Assert.Equal(ScanOverwritePolicy.Overwrite, viewModel.OverwritePolicy);
    }

    // 16. Verbose toggle.
    [Fact]
    public void Verbose_Toggles()
    {
        var viewModel = Build();
        Assert.False(viewModel.Verbose);
        viewModel.Verbose = true;
        Assert.True(viewModel.Verbose);
    }

    // 17. Validation errors.
    [Fact]
    public void InvalidConfiguration_PopulatesValidationErrors()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = string.Empty;

        viewModel.ValidateCommand.Execute(null);

        Assert.False(viewModel.IsValid);
        Assert.NotEmpty(viewModel.ValidationErrors);
    }

    // 18. Successful validation.
    [Fact]
    public void ValidConfiguration_ProducesNoValidationErrors()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = "./out";

        viewModel.ValidateCommand.Execute(null);

        Assert.True(viewModel.IsValid);
        Assert.Empty(viewModel.ValidationErrors);
    }

    // 19. Credential values never appear in validation messages.
    [Fact]
    public void PasswordValue_NeverAppearsInValidationErrors()
    {
        const string sentinelPassword = "SERVER_SLEUTH_TEST_GUI_VM_PASSWORD_2c7e";
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Windows;
        viewModel.RemoteHost = "winhost";
        using var password = Secure(sentinelPassword);
        viewModel.SetPassword(password);
        // Deliberately leave Username empty too, to maximize the chance a naive implementation
        // would echo *something* about the credential into an error message.

        viewModel.ValidateCommand.Execute(null);

        Assert.DoesNotContain(viewModel.ValidationErrors, e => e.Message.Contains(sentinelPassword, StringComparison.Ordinal));
    }

    // 20. Credentials not stored in GuiApplicationState — proven structurally: this ViewModel
    // has no dependency on IApplicationStateService at all, so it has no way to write to it.
    [Fact]
    public void ScanConfigurationViewModel_HasNoDependencyOnApplicationStateService()
    {
        var constructor = Assert.Single(typeof(ScanConfigurationViewModel).GetConstructors());
        Assert.DoesNotContain(constructor.GetParameters(), p => p.ParameterType == typeof(IApplicationStateService));
    }

    // 21 & 23. Start Scan with a valid configuration raises ScanRequested with the expected data — never executes anything.
    [Fact]
    public void StartScan_WithValidConfiguration_RaisesScanRequested_WithTheExpectedTarget()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = "./out";
        viewModel.OutputFormat = ScanOutputFormat.Html;

        ScanRequest? captured = null;
        viewModel.ScanRequested += (_, args) => captured = args.Request;

        viewModel.StartScanCommand.Execute(null);

        Assert.NotNull(captured);
        Assert.Equal(TargetKind.Local, captured!.Target.Kind);
        Assert.Equal(ScanOutputFormat.Html, captured.OutputFormat);
    }

    // 22. Start Scan with an invalid configuration never raises ScanRequested.
    [Fact]
    public void StartScan_WithInvalidConfiguration_NeverRaisesScanRequested()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = string.Empty; // invalid

        var raised = false;
        viewModel.ScanRequested += (_, _) => raised = true;

        viewModel.StartScanCommand.Execute(null);

        Assert.False(raised);
        Assert.False(viewModel.IsValid);
    }

    // 24. Repeated request creation produces equivalent results.
    [Fact]
    public void StartScan_CalledTwiceWithIdenticalConfiguration_ProducesEquivalentRequests()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = "./out";

        var captured = new List<ScanRequest>();
        viewModel.ScanRequested += (_, args) => captured.Add(args.Request);

        viewModel.StartScanCommand.Execute(null);
        viewModel.StartScanCommand.Execute(null);

        Assert.Equal(2, captured.Count);
        Assert.Equal(captured[0].Target, captured[1].Target);
        Assert.Equal(captured[0].OutputDirectory, captured[1].OutputDirectory);
    }

    // 25. No filesystem/network side effects — validation and request creation complete near-instantly.
    [Fact]
    public void Validate_AndStartScan_CompleteQuickly_NoFilesystemOrNetworkAccess()
    {
        var viewModel = Build();
        viewModel.TargetKind = TargetKind.Remote;
        viewModel.Platform = TargetPlatform.Linux;
        viewModel.RemoteHost = "some-host-that-does-not-exist.invalid";
        viewModel.SshPrivateKeyPath = "/nonexistent/path/to/key";
        viewModel.SshHostFingerprint = "aa:bb";
        viewModel.Username = "tester";
        viewModel.OutputDirectory = "./out";

        var stopwatch = Stopwatch.StartNew();
        viewModel.ValidateCommand.Execute(null);
        viewModel.StartScanCommand.Execute(null);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Validation/request creation took {stopwatch.ElapsedMilliseconds}ms — expected near-instant, no network/filesystem access.");
    }

    // Cancel returns to a safe state.
    [Fact]
    public void Cancel_ClearsValidationState_AndCredentials()
    {
        var viewModel = Build();
        viewModel.OutputDirectory = string.Empty;
        viewModel.ValidateCommand.Execute(null);
        Assert.NotEmpty(viewModel.ValidationErrors);

        viewModel.CancelCommand.Execute(null);

        Assert.Empty(viewModel.ValidationErrors);
        Assert.False(viewModel.IsValid);
        Assert.Null(viewModel.Username);
    }
}
