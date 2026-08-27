using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;

namespace ServerSleuth.Cli.Tests;

/// <summary>--help / --version / unknown-command behavior — see skill.md (Phase 10A) §5, §13-14.</summary>
public class CliApplicationHelpTests
{
    [Fact]
    public async Task Help_PrintsUsage_ExitsSuccess()
    {
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync(["--help"]);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("serversleuth scan", stdout, StringComparison.Ordinal);
        Assert.Contains("Usage:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoArguments_PrintsUsage_ExitsSuccess()
    {
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync([]);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("Usage:", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanHelp_DocumentsAllOptions()
    {
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync(["scan", "--help"]);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("--output", stdout, StringComparison.Ordinal);
        Assert.Contains("--format", stdout, StringComparison.Ordinal);
        Assert.Contains("--overwrite", stdout, StringComparison.Ordinal);
        Assert.Contains("--quiet", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_PrintsAssemblyVersion_ExitsSuccess()
    {
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync(["--version"]);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout));
        Assert.Matches(@"^\d+\.\d+\.\d+", stdout.Trim());
    }

    [Fact]
    public async Task UnknownCommand_PrintsErrorToStderr_ExitsInvalidArguments()
    {
        var (exitCode, _, stderr) = await CliTestRunner.RunAsync(["frobnicate"]);

        Assert.Equal(CliExitCode.InvalidArguments, exitCode);
        Assert.Contains("Unknown command", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Help_IsDeterministic_AcrossRepeatedInvocations()
    {
        var first = await CliTestRunner.RunAsync(["--help"]);
        var second = await CliTestRunner.RunAsync(["--help"]);

        Assert.Equal(first.Stdout, second.Stdout, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ScanHelp_IsDeterministic_AcrossRepeatedInvocations()
    {
        var first = await CliTestRunner.RunAsync(["scan", "--help"]);
        var second = await CliTestRunner.RunAsync(["scan", "--help"]);

        Assert.Equal(first.Stdout, second.Stdout, StringComparer.Ordinal);
    }
}
