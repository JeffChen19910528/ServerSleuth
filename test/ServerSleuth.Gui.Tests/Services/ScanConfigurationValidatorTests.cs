using System.Security;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

/// <summary>GUI-2 §Step7, §Step12: deterministic, side-effect-free scan-configuration
/// validation.</summary>
public class ScanConfigurationValidatorTests
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

    private readonly ScanConfigurationValidator _validator = new();

    [Fact]
    public void Local_WithAnOutputDirectory_IsValid()
    {
        var config = ScanConfigurationState.Initial with { OutputDirectory = "./out" };
        var result = _validator.Validate(config, ScanCredentialInput.Empty);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Local_DefaultConfiguration_IsInvalid_OnlyBecauseNoOutputDirectoryWasChosenYet()
    {
        // ScanConfigurationState.Initial has an empty OutputDirectory — a required field with no
        // sensible default (skill.md GUI-2 §6: "reject empty path when output is required").
        var result = _validator.Validate(ScanConfigurationState.Initial, ScanCredentialInput.Empty);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(nameof(ScanConfigurationState.OutputDirectory), result.Errors[0].Field);
    }

    [Fact]
    public void Remote_EmptyHost_IsInvalid()
    {
        var config = ScanConfigurationState.Initial with { TargetKind = TargetKind.Remote, Platform = TargetPlatform.Linux, TransportKind = ScanTransportKind.Ssh, SshPrivateKeyPath = "/key", SshHostFingerprint = "aa:bb" };
        var credentials = new ScanCredentialInput { Username = "tester" };

        var result = _validator.Validate(config, credentials);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanConfigurationState.RemoteHost));
    }

    [Fact]
    public void Remote_ValidHost_DoesNotProduceAHostError()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "db-server-1", Platform = TargetPlatform.Linux,
            TransportKind = ScanTransportKind.Ssh, SshPrivateKeyPath = "/key", SshHostFingerprint = "aa:bb"
        };
        var credentials = new ScanCredentialInput { Username = "tester" };

        var result = _validator.Validate(config, credentials);

        Assert.DoesNotContain(result.Errors, e => e.Field == nameof(ScanConfigurationState.RemoteHost));
    }

    [Fact]
    public void RemoteLinux_WithWinRmTransport_IsInvalid()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Linux, TransportKind = ScanTransportKind.WinRm
        };

        var result = _validator.Validate(config, ScanCredentialInput.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanConfigurationState.TransportKind));
    }

    [Fact]
    public void RemoteWindows_WithSshTransport_IsInvalid()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Windows, TransportKind = ScanTransportKind.Ssh
        };

        var result = _validator.Validate(config, ScanCredentialInput.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanConfigurationState.TransportKind));
    }

    [Fact]
    public void RemoteLinux_MissingPrivateKeyPathOrHostFingerprint_IsInvalid()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Linux, TransportKind = ScanTransportKind.Ssh
        };
        var credentials = new ScanCredentialInput { Username = "tester" };

        var result = _validator.Validate(config, credentials);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanConfigurationState.SshPrivateKeyPath));
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanConfigurationState.SshHostFingerprint));
    }

    [Fact]
    public void RemoteLinux_FullyConfigured_IsValid()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Linux, TransportKind = ScanTransportKind.Ssh,
            SshPrivateKeyPath = "/home/user/.ssh/id_ed25519", SshHostFingerprint = "aa:bb:cc",
            OutputDirectory = "./out"
        };
        var credentials = new ScanCredentialInput { Username = "tester" };

        var result = _validator.Validate(config, credentials);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RemoteWindows_MissingUsernameOrPassword_IsInvalid()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Windows, TransportKind = ScanTransportKind.WinRm
        };

        var result = _validator.Validate(config, ScanCredentialInput.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanCredentialInput.Username));
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanCredentialInput.Password));
    }

    [Fact]
    public void RemoteWindows_FullyConfigured_IsValid()
    {
        using var password = Secure("DeliberatelySensitiveTestPassword1");
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Windows, TransportKind = ScanTransportKind.WinRm,
            OutputDirectory = "./out"
        };
        var credentials = new ScanCredentialInput { Username = "svc-account", Password = password };

        var result = _validator.Validate(config, credentials);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EmptyOutputDirectory_IsInvalid()
    {
        var config = ScanConfigurationState.Initial with { OutputDirectory = string.Empty };
        var result = _validator.Validate(config, ScanCredentialInput.Empty);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(ScanConfigurationState.OutputDirectory));
    }

    [Theory]
    [InlineData(ScanOutputFormat.Json)]
    [InlineData(ScanOutputFormat.Html)]
    [InlineData(ScanOutputFormat.Both)]
    public void EveryOutputFormat_IsValid_ForALocalTarget(ScanOutputFormat format)
    {
        var config = ScanConfigurationState.Initial with { OutputDirectory = "./out", OutputFormat = format };
        var result = _validator.Validate(config, ScanCredentialInput.Empty);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PasswordValue_NeverAppearsInAnyValidationErrorMessage()
    {
        const string sentinelPassword = "SERVER_SLEUTH_TEST_GUI_PASSWORD_a91f";
        using var password = Secure(sentinelPassword);

        // Deliberately missing username too, so the WinRM branch produces errors that MIGHT
        // have been tempted to echo the credential back for "helpfulness."
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Windows, TransportKind = ScanTransportKind.WinRm
        };
        var credentials = new ScanCredentialInput { Password = password };

        var result = _validator.Validate(config, credentials);

        Assert.DoesNotContain(result.Errors, e => e.Message.Contains(sentinelPassword, StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatedIdenticalValidation_IsDeterministic()
    {
        var config = ScanConfigurationState.Initial with { OutputDirectory = "./out" };

        var first = _validator.Validate(config, ScanCredentialInput.Empty);
        var second = _validator.Validate(config, ScanCredentialInput.Empty);

        Assert.Equal(first.IsValid, second.IsValid);
        Assert.Equal(first.Errors.Count, second.Errors.Count);
    }
}
