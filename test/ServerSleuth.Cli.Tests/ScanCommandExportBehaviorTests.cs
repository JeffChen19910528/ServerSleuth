using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>Overwrite policy, existing-report, and invalid-output-directory behavior — see
/// skill.md (Phase 10A) §6, §15, §17, §20.</summary>
public class ScanCommandExportBehaviorTests
{
    [Fact]
    public async Task ExistingReport_WithoutOverwrite_FailsWithExportFailureExitCode()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "report.json"), "{}");

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var (exitCode, _, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "json"], engine);

        Assert.Equal(CliExitCode.ExportFailure, exitCode);
        Assert.NotEmpty(stderr);
    }

    [Fact]
    public async Task ExistingReport_WithOverwrite_Succeeds_ReplacesContent()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "report.json"), "{\"stale\":true}");

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var (exitCode, _, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "json", "--overwrite"], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        var content = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        Assert.DoesNotContain("stale", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidOutputDirectory_CollidingWithAFile_ReturnsExportFailure()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(temp.Path)!);
        await File.WriteAllTextAsync(temp.Path, "not a directory");

        try
        {
            var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
            var (exitCode, _, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

            Assert.Equal(CliExitCode.ExportFailure, exitCode);
            Assert.NotEmpty(stderr);
        }
        finally
        {
            File.Delete(temp.Path);
        }
    }

    [Fact]
    public async Task InvalidArguments_NeverReachTheExporter_NoFilesWritten()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, _, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "xml"], engine);

        Assert.Equal(CliExitCode.InvalidArguments, exitCode);
        Assert.NotEmpty(stderr);
        Assert.False(Directory.Exists(temp.Path));
    }
}
