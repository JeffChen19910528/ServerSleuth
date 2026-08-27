using ServerSleuth.Linux.Cron;

namespace ServerSleuth.Linux.Tests.Cron;

public class CronLineParserTests
{
    [Fact]
    public void ParseSystemCrontabLine_TypicalLine_ExtractsScheduleUserAndCommand()
    {
        var entry = CronLineParser.ParseSystemCrontabLine("17 *\t* * *\troot\tcd / && run-parts --report /etc/cron.hourly");

        Assert.NotNull(entry);
        Assert.Equal("17 * * * *", entry!.Schedule);
        Assert.Equal("root", entry.User);
        Assert.Equal("cd / && run-parts --report /etc/cron.hourly", entry.Command);
    }

    [Fact]
    public void ParseSystemCrontabLine_MacroForm_IsRecognized()
    {
        var entry = CronLineParser.ParseSystemCrontabLine("@reboot root /opt/erp/bin/startup.sh");

        Assert.NotNull(entry);
        Assert.Equal("@reboot", entry!.Schedule);
        Assert.Equal("root", entry.User);
        Assert.Equal("/opt/erp/bin/startup.sh", entry.Command);
    }

    [Fact]
    public void ParseUserCrontabLine_TypicalLine_HasNoUserField()
    {
        var entry = CronLineParser.ParseUserCrontabLine("0 2 * * * /opt/erp/bin/nightly-backup.sh --full");

        Assert.NotNull(entry);
        Assert.Equal("0 2 * * *", entry!.Schedule);
        Assert.Null(entry.User);
        Assert.Equal("/opt/erp/bin/nightly-backup.sh --full", entry.Command);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# a comment line")]
    [InlineData("PATH=/usr/bin:/bin")]
    [InlineData("MAILTO=\"\"")]
    public void ParseUserCrontabLine_SkippableLines_ReturnNull(string line)
    {
        Assert.Null(CronLineParser.ParseUserCrontabLine(line));
    }

    [Fact]
    public void ParseUserCrontabLine_TooFewFields_ReturnsNull_NeverGuesses()
    {
        Assert.Null(CronLineParser.ParseUserCrontabLine("0 2 * *"));
    }

    [Fact]
    public void ParseSystemCrontabLine_TooFewFields_ReturnsNull_NeverGuesses()
    {
        Assert.Null(CronLineParser.ParseSystemCrontabLine("0 2 * * * root"));
    }
}
