using ServerSleuth.Linux.Cron;

namespace ServerSleuth.Linux.Tests.Cron;

public class CronCommandPathExtractorTests
{
    [Fact]
    public void TryExtractExecutablePath_ExplicitAbsolutePath_IsExtracted()
    {
        Assert.Equal("/opt/erp/bin/worker", CronCommandPathExtractor.TryExtractExecutablePath("/opt/erp/bin/worker --flag arg"));
    }

    [Fact]
    public void TryExtractExecutablePath_BareCommandName_IsUnresolved_NeverGuessesViaPath()
    {
        Assert.Null(CronCommandPathExtractor.TryExtractExecutablePath("python3 /opt/erp/script.py"));
    }

    [Fact]
    public void TryExtractExecutablePath_ShellChaining_IsUnresolved_NeverEvaluatesShellSyntax()
    {
        Assert.Null(CronCommandPathExtractor.TryExtractExecutablePath("cd / && /opt/erp/bin/worker"));
    }

    [Fact]
    public void TryExtractExecutablePath_NoArguments_ReturnsWholeCommand()
    {
        Assert.Equal("/opt/erp/bin/worker", CronCommandPathExtractor.TryExtractExecutablePath("/opt/erp/bin/worker"));
    }
}
