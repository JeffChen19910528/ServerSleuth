using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Cli.Tests;

/// <summary>Partial discovery / scanner failure never aborts the scan — see skill.md
/// (Phase 10A) §16.</summary>
public class ScanCommandPartialDiscoveryTests
{
    [Fact]
    public async Task AccessDeniedScanner_StillProducesAFullReport_WithPartialDiscoveryExitCode()
    {
        using var temp = new TempDirectory();
        var discovery = DiscoveryResultBuilder.Build(
            ErpFixture.BuildEntities(),
            new DiscoveryResult
            {
                ScannerId = "windows-iis-scanner",
                Status = ScannerStatus.AccessDenied,
                Errors = [new DiscoveryError { ScannerId = "windows-iis-scanner", Message = "Access denied." }]
            });

        var engine = new FakeDiscoveryEngine(discovery);
        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.Equal(CliExitCode.PartialDiscovery, exitCode);
        Assert.Empty(stderr);
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.Contains("Partial:  1", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedScanner_StillProducesAFullReport_WithPartialDiscoveryExitCode()
    {
        using var temp = new TempDirectory();
        var discovery = DiscoveryResultBuilder.Build(
            ErpFixture.BuildEntities(),
            new DiscoveryResult { ScannerId = "windows-com-scanner", Status = ScannerStatus.Failed });

        var engine = new FakeDiscoveryEngine(discovery);
        var (exitCode, _, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.Equal(CliExitCode.PartialDiscovery, exitCode);
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json")));
    }

    [Fact]
    public async Task NotApplicableAndNotInstalledScanners_AreNeutral_NotPartial()
    {
        using var temp = new TempDirectory();
        var discovery = DiscoveryResultBuilder.Build(
            ErpFixture.BuildEntities(),
            new DiscoveryResult { ScannerId = "linux-container-scanner", Status = ScannerStatus.NotApplicable },
            new DiscoveryResult { ScannerId = "linux-package-scanner", Status = ScannerStatus.NotInstalled });

        var engine = new FakeDiscoveryEngine(discovery);
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("Partial:  0", stdout, StringComparison.Ordinal);
    }
}
