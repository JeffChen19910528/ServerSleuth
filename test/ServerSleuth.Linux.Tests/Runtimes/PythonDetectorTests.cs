using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Runtimes.Detectors;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Runtimes;

public class PythonDetectorTests
{
    [Fact]
    public async Task DetectAsync_MultipleInterpreters_ProducesOneRowPerDistinctExecutable_NeverMerged()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/usr/bin", "/usr/bin/python3", "/usr/bin/python3.10", "/usr/bin/python3.11");
        fs.SetFileEntries("/usr/local/bin");
        fs.SetFileEntries("/opt/python/bin");

        var locator = new FakeExecutableLocator(); // direct PATH lookup finds nothing extra here

        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/bin/python3", ["--version"], ProcessResult.Ok(0, "Python 3.11.6\n", "", TimeSpan.Zero));
        runner.SetResult("/usr/bin/python3.10", ["--version"], ProcessResult.Ok(0, "Python 3.10.13\n", "", TimeSpan.Zero));
        runner.SetResult("/usr/bin/python3.11", ["--version"], ProcessResult.Ok(0, "Python 3.11.6\n", "", TimeSpan.Zero));

        var result = await new PythonDetector(locator, runner, fs).DetectAsync(CancellationToken.None);

        Assert.Equal(3, result.Rows.Count); // 3 distinct executable paths -> 3 entities, even though two share a version
        Assert.Contains(result.Rows, r => r.ExecutablePath == "/usr/bin/python3.10");
        Assert.Contains(result.Rows, r => r.ExecutablePath == "/usr/bin/python3.11");
    }

    [Fact]
    public async Task DetectAsync_NoInterpretersFound_ReturnsNotDetected()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/usr/bin");
        fs.SetFileEntries("/usr/local/bin");
        fs.SetFileEntries("/opt/python/bin");

        var result = await new PythonDetector(new FakeExecutableLocator(), new FakeProcessRunner(), fs).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task DetectAsync_ResolvedPathThatCannotRun_IsExcludedNotReported()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileEntries("/usr/bin", "/usr/bin/python3-stub");
        fs.SetFileEntries("/usr/local/bin");
        fs.SetFileEntries("/opt/python/bin");

        var runner = new FakeProcessRunner(); // "python3-stub --version" not registered -> fails

        var result = await new PythonDetector(new FakeExecutableLocator(), runner, fs).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }
}
