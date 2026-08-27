using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Options;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Phase 10C §11 established: <c>local</c>/omitted must behave identically, and an unsupported
/// target must be rejected CLEARLY. Phase 10D-2 §6 fills in a real meaning for a non-'local'
/// value — a remote SSH host — which now requires <c>--ssh-user</c>/<c>--ssh-key</c>/
/// <c>--ssh-host-fingerprint</c>; omitting any of them is still a clear, immediate parse-time
/// rejection (never a silent fallback to local, never an attempted connection).
/// </summary>
public class ScanCommandTargetOptionTests
{
    [Fact]
    public void Parse_TargetLocal_Succeeds_CaseInsensitive()
    {
        var options = ScanOptionsParser.Parse(["--target", "LOCAL"]);
        Assert.NotNull(options); // no exception — 'local' is accepted regardless of case
        Assert.Null(options.Remote);
    }

    [Fact]
    public void Parse_TargetMissingValue_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target"]));
    }

    [Fact]
    public void Parse_EmptyTarget_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", ""]));
    }

    [Theory]
    [InlineData("server1")]
    [InlineData("192.168.1.10")]
    [InlineData("remote")]
    public void Parse_RemoteTargetWithNoSshOptions_ThrowsCliArgumentException_MentioningWhatIsMissing(string value)
    {
        var ex = Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", value]));
        Assert.Contains("--ssh-user", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RemoteTargetMissingSshKey_ThrowsCliArgumentException_MentioningSshKey()
    {
        var ex = Assert.Throws<CliArgumentException>(() =>
            ScanOptionsParser.Parse(["--target", "server1", "--ssh-user", "alice"]));
        Assert.Contains("--ssh-key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RemoteTargetMissingHostFingerprint_ThrowsCliArgumentException_MentioningFingerprint()
    {
        var ex = Assert.Throws<CliArgumentException>(() =>
            ScanOptionsParser.Parse(["--target", "server1", "--ssh-user", "alice", "--ssh-key", "/tmp/key"]));
        Assert.Contains("--ssh-host-fingerprint", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_FullyySpecifiedRemoteTarget_Succeeds_AndCapturesEveryOption()
    {
        var options = ScanOptionsParser.Parse([
            "--target", "server1", "--ssh-user", "alice", "--ssh-key", "/home/alice/.ssh/id_ed25519",
            "--ssh-port", "2222", "--ssh-key-passphrase-env", "SERVERSLEUTH_SSH_PASSPHRASE",
            "--ssh-host-fingerprint", "aa:bb:cc"
        ]);

        Assert.NotNull(options.Remote);
        Assert.Equal("server1", options.Remote!.Host);
        Assert.Equal("alice", options.Remote.Username);
        Assert.Equal("/home/alice/.ssh/id_ed25519", options.Remote.PrivateKeyPath);
        Assert.Equal(2222, options.Remote.Port);
        Assert.Equal("SERVERSLEUTH_SSH_PASSPHRASE", options.Remote.PrivateKeyPassphraseEnvironmentVariable);
        Assert.Equal("aa:bb:cc", options.Remote.HostFingerprint);
    }

    [Fact]
    public void Parse_InvalidSshPort_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", "server1", "--ssh-port", "not-a-number"]));
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--target", "server1", "--ssh-port", "99999"]));
    }

    [Fact]
    public async Task ScanCommand_TargetLocal_BehavesExactlyLikeNoTargetOption_SameExitCode()
    {
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();

        var engineA = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var engineB = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCodeDefault, _, _) = await CliTestRunner.RunAsync(["scan", "--output", tempA.Path], engineA);
        var (exitCodeExplicitLocal, _, _) = await CliTestRunner.RunAsync(["scan", "--output", tempB.Path, "--target", "local"], engineB);

        Assert.Equal(CliExitCode.Success, exitCodeDefault);
        Assert.Equal(exitCodeDefault, exitCodeExplicitLocal);
    }

    [Fact]
    public async Task ScanCommand_RemoteTargetMissingSshOptions_NeverReachesDiscovery_ExitsInvalidArguments()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--target", "remote-server"], engine);

        Assert.Equal(CliExitCode.InvalidArguments, exitCode);
        Assert.Contains("--ssh-user", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json"))); // discovery/export never ran, no network contact
        Assert.Empty(stdout);
    }

    [Fact]
    public async Task Verbose_PrintsTargetIdentity_LocalOnly()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--verbose"], engine);

        Assert.Contains("Target: local (", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonVerbose_NeverPrintsTargetLine()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.DoesNotContain("Target:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanHelp_DocumentsTargetOption()
    {
        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--help"]);
        Assert.Contains("--target", stdout, StringComparison.Ordinal);
    }
}
