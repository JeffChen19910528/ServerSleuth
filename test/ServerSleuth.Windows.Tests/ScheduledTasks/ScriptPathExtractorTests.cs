using ServerSleuth.Windows.ScheduledTasks;

namespace ServerSleuth.Windows.Tests.ScheduledTasks;

public class ScriptPathExtractorTests
{
    [Theory]
    [InlineData("powershell.exe", true)]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", true)]
    [InlineData("pwsh.exe", true)]
    [InlineData("cscript.exe", true)]
    [InlineData("wscript.exe", true)]
    [InlineData("cmd.exe", true)]
    [InlineData(@"D:\ERP\NightlyJob.exe", false)]
    [InlineData(null, false)]
    public void IsScriptHost_IdentifiesKnownScriptHosts(string? path, bool expected)
    {
        Assert.Equal(expected, ScriptPathExtractor.IsScriptHost(path));
    }

    [Fact]
    public void TryExtract_QuotedScriptPath_ReturnsPath()
    {
        var result = ScriptPathExtractor.TryExtract(@"-File ""D:\Scripts\NightlyJob.ps1"" -Sync");

        Assert.Equal(@"D:\Scripts\NightlyJob.ps1", result);
    }

    [Fact]
    public void TryExtract_BareScriptPath_ReturnsPath()
    {
        var result = ScriptPathExtractor.TryExtract(@"D:\Scripts\NightlyJob.ps1");

        Assert.Equal(@"D:\Scripts\NightlyJob.ps1", result);
    }

    [Theory]
    [InlineData(".vbs")]
    [InlineData(".js")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    public void TryExtract_OtherScriptExtensions_AreRecognized(string extension)
    {
        var result = ScriptPathExtractor.TryExtract($@"D:\Scripts\Job{extension}");

        Assert.Equal($@"D:\Scripts\Job{extension}", result);
    }

    [Fact]
    public void TryExtract_NoScriptFileReferenced_ReturnsNull()
    {
        var result = ScriptPathExtractor.TryExtract("--sync --verbose");

        Assert.Null(result);
    }

    [Fact]
    public void TryExtract_NullArguments_ReturnsNull()
    {
        Assert.Null(ScriptPathExtractor.TryExtract(null));
    }
}
