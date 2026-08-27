using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Runtimes.Detectors;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Runtimes;

public class JavaDetectorTests
{
    [Fact]
    public async Task DetectAsync_JdkPresent_DetectsJdkViaJavacPresence()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("java", "/usr/lib/jvm/java-17-openjdk/bin/java");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/lib/jvm/java-17-openjdk/bin/java", ["-version"],
            ProcessResult.Ok(0, "", "openjdk version \"17.0.9\" 2023-10-17\nOpenJDK Runtime Environment (build 17.0.9+9)\n", TimeSpan.Zero));

        var fs = new FakeFileSystemReader();
        fs.SetText("/usr/lib/jvm/java-17-openjdk/bin/javac", string.Empty); // presence marks it a JDK

        var result = await new JavaDetector(locator, runner, fs).DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Java (JDK)", row.Name);
        Assert.Equal("17.0.9", row.Version);
        Assert.Equal("OpenJDK", row.Edition);
    }

    [Fact]
    public async Task DetectAsync_JreOnly_NoJavac_DetectsAsJre()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("java", "/usr/lib/jvm/java-17-openjdk/bin/java");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/lib/jvm/java-17-openjdk/bin/java", ["-version"],
            ProcessResult.Ok(0, "", "openjdk version \"17.0.9\" 2023-10-17\n", TimeSpan.Zero));

        var result = await new JavaDetector(locator, runner, new FakeFileSystemReader()).DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Java (JRE)", row.Name);
    }

    [Fact]
    public async Task DetectAsync_JavaNotOnPath_ReturnsNotDetected()
    {
        var result = await new JavaDetector(new FakeExecutableLocator(), new FakeProcessRunner(), new FakeFileSystemReader())
            .DetectAsync(CancellationToken.None);

        Assert.Equal(Core.Enums.ScannerStatus.NotInstalled, result.Status);
    }
}
