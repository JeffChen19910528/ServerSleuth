using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Cli.Tests;

/// <summary>--verbose surfaces real per-scanner Id/Status/entity-count and stage durations — see
/// skill.md (Phase 10B) §5-6, §13. Every value asserted here comes from the same
/// <see cref="DiscoveryResult"/> data the fake engine was built with — nothing is fabricated by
/// the CLI, and non-verbose runs must never show this detail.</summary>
public class ScanCommandVerboseModeTests
{
    [Fact]
    public async Task Verbose_PrintsRealPerScannerStatusAndEntityCounts()
    {
        using var temp = new TempDirectory();
        var discovery = DiscoveryResultBuilder.Build(
            ErpFixture.BuildEntities(),
            new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.AccessDenied },
            new DiscoveryResult { ScannerId = "windows-com-scanner", Status = ScannerStatus.Supported, Entities = ErpFixture.BuildEntities() });

        var engine = new FakeDiscoveryEngine(discovery);
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--verbose"], engine);

        Assert.Equal(CliExitCode.PartialDiscovery, exitCode);
        Assert.Contains("Scanning:", stdout, StringComparison.Ordinal);
        Assert.Contains("windows-iis-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("AccessDenied", stdout, StringComparison.Ordinal);
        Assert.Contains("windows-com-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("Supported", stdout, StringComparison.Ordinal);
        // windows-com-scanner's Entities list was built from the same 17-entity ERP fixture.
        Assert.Contains($"windows-com-scanner{new string(' ', 32 - "windows-com-scanner".Length)} Supported          17", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verbose_PrintsStageDurations()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--verbose"], engine);

        Assert.Matches(@"Duration: \d+\.\d{2}s", stdout);
    }

    [Fact]
    public async Task NonVerbose_NeverPrintsScannerBreakdownOrDurations()
    {
        using var temp = new TempDirectory();
        var discovery = DiscoveryResultBuilder.Build(
            ErpFixture.BuildEntities(),
            new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.AccessDenied });

        var engine = new FakeDiscoveryEngine(discovery);
        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.DoesNotContain("Scanning:", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Duration:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verbose_NeverFabricatesACountForAScannerThatFoundNothing()
    {
        using var temp = new TempDirectory();
        var discovery = DiscoveryResultBuilder.Build(
            ErpFixture.BuildEntities(),
            new DiscoveryResult { ScannerId = "linux-docker-scanner", Status = ScannerStatus.NotInstalled, Entities = [] });

        var engine = new FakeDiscoveryEngine(discovery);
        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--verbose"], engine);

        Assert.Contains("linux-docker-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("NotInstalled", stdout, StringComparison.Ordinal);
        Assert.Contains($"linux-docker-scanner{new string(' ', 32 - "linux-docker-scanner".Length)} NotInstalled       0", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuietAndVerboseTogether_QuietWins_NoProgressOutputAtAll()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--quiet", "--verbose"], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Empty(stdout);
        Assert.Empty(stderr);
    }

    [Fact]
    public async Task ScanHelp_DocumentsVerboseOption()
    {
        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--help"]);
        Assert.Contains("--verbose", stdout, StringComparison.Ordinal);
    }
}
