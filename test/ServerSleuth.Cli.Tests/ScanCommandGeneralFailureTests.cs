using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// The one previously-untested exit code (Phase 10B §8, §22-G: "add tests for every defined
/// exit code"). <see cref="CliExitCode.GeneralFailure"/> is reserved for an unexpected exception
/// from a layer the CLI composes — never one of the more specific, anticipated failures
/// (invalid arguments, export failure, partial discovery, cancellation) that already have their
/// own dedicated exit codes and tests.
/// </summary>
public class ScanCommandGeneralFailureTests
{
    [Fact]
    public async Task UnexpectedException_ReturnsGeneralFailureExitCode_ConciseMessage_NoStackTrace()
    {
        using var temp = new TempDirectory();
        var engine = new ThrowingFakeDiscoveryEngine(new InvalidOperationException("simulated unexpected discovery failure"));

        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.Equal(CliExitCode.GeneralFailure, exitCode);
        Assert.Contains("simulated unexpected discovery failure", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("at ServerSleuth", stderr, StringComparison.Ordinal); // no stack trace
        Assert.DoesNotContain("simulated unexpected discovery failure", stdout, StringComparison.Ordinal); // error goes to stderr, never stdout
        Assert.False(File.Exists(Path.Combine(temp.Path, "report.json")));
    }

    [Fact]
    public async Task UnexpectedException_NeverWritesAPartialReport()
    {
        using var temp = new TempDirectory();
        var engine = new ThrowingFakeDiscoveryEngine(new InvalidOperationException("boom"));

        await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.False(Directory.Exists(temp.Path) && Directory.EnumerateFileSystemEntries(temp.Path).Any());
    }
}
