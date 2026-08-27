using ServerSleuth.Cli.Options;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Phase 10D-3B §8: a Windows/WinRM remote target requires <c>--winrm-user</c>/
/// <c>--winrm-password-env</c>, mirroring <see cref="ScanCommandTargetOptionTests"/>'s own SSH
/// option tests exactly — rejected at parse time, before any I/O or network activity, if either
/// is missing; a password is only ever accepted via an environment-variable NAME.
/// </summary>
public class ScanCommandWindowsRemoteOptionTests
{
    [Fact]
    public void Parse_RemoteWindowsTargetMissingPasswordEnv_ThrowsCliArgumentException_MentioningPasswordEnv()
    {
        var ex = Assert.Throws<CliArgumentException>(() =>
            ScanOptionsParser.Parse(["--target", "winhost", "--winrm-user", "svc-account"]));
        Assert.Contains("--winrm-password-env", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_FullySpecifiedWindowsRemoteTarget_Succeeds_AndCapturesEveryOption()
    {
        var options = ScanOptionsParser.Parse([
            "--target", "winhost", "--winrm-user", "svc-account", "--winrm-password-env", "SERVERSLEUTH_WINRM_PASSWORD",
            "--winrm-domain", "CORP", "--winrm-port", "5986", "--winrm-auth", "kerberos"
        ]);

        Assert.Null(options.Remote);
        Assert.NotNull(options.WindowsRemote);
        Assert.Equal("winhost", options.WindowsRemote!.Host);
        Assert.Equal("svc-account", options.WindowsRemote.Username);
        Assert.Equal("SERVERSLEUTH_WINRM_PASSWORD", options.WindowsRemote.PasswordEnvironmentVariable);
        Assert.Equal("CORP", options.WindowsRemote.Domain);
        Assert.Equal(5986, options.WindowsRemote.Port);
        Assert.Equal("kerberos", options.WindowsRemote.AuthenticationMechanism);
        Assert.True(options.WindowsRemote.UseSsl);
    }

    [Fact]
    public void Parse_WinRmNoSsl_SetsUseSslFalse()
    {
        var options = ScanOptionsParser.Parse([
            "--target", "winhost", "--winrm-user", "svc-account", "--winrm-password-env", "SERVERSLEUTH_WINRM_PASSWORD", "--winrm-no-ssl"
        ]);

        Assert.False(options.WindowsRemote!.UseSsl);
    }

    [Fact]
    public void Parse_InvalidWinRmAuth_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", "winhost", "--winrm-auth", "basic"]));
    }

    [Fact]
    public void Parse_InvalidWinRmPort_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", "winhost", "--winrm-port", "not-a-number"]));
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", "winhost", "--winrm-port", "99999"]));
    }

    [Fact]
    public void Parse_BothSshUserAndWinRmUser_ThrowsCliArgumentException_Ambiguous()
    {
        var ex = Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse([
            "--target", "ambiguous-host", "--ssh-user", "alice", "--winrm-user", "svc-account"
        ]));
        Assert.Contains("both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WinRmUserWithoutRemoteTarget_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--winrm-user", "svc-account", "--winrm-password-env", "VAR"]));
    }
}
