using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>--quiet suppresses progress, never errors — see skill.md (Phase 10A) §6, §22.</summary>
public class ScanCommandQuietModeTests
{
    [Fact]
    public async Task Quiet_SuppressesProgressOutput_OnSuccess()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--quiet"], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Empty(stdout);
        Assert.Empty(stderr);
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json")));
    }

    [Fact]
    public async Task Quiet_StillPrintsErrors_OnExportFailure()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "report.json"), "{}");

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "json", "--quiet"], engine);

        Assert.Equal(CliExitCode.ExportFailure, exitCode);
        Assert.Empty(stdout);
        Assert.NotEmpty(stderr);
    }
}
