using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Runtimes.Detectors;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Runtimes;

public class NodePhpGoDetectorTests
{
    [Fact]
    public async Task NodeDetector_NodeAndNpmBothPresent_ProducesTwoRows()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("node", "/usr/bin/node");
        locator.SetPath("npm", "/usr/bin/npm");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/bin/node", ["--version"], ProcessResult.Ok(0, "v20.11.0\n", "", TimeSpan.Zero));
        runner.SetResult("/usr/bin/npm", ["--version"], ProcessResult.Ok(0, "10.2.4\n", "", TimeSpan.Zero));

        var result = await new NodeDetector(locator, runner).DetectAsync(CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Family == "Node" && r.Version == "20.11.0");
        Assert.Contains(result.Rows, r => r.Family == "Npm" && r.Version == "10.2.4");
    }

    [Fact]
    public async Task NodeDetector_NeitherPresent_ReturnsNotDetected()
    {
        var result = await new NodeDetector(new FakeExecutableLocator(), new FakeProcessRunner()).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task PhpDetector_TypicalVersion_IsDetected()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("php", "/usr/bin/php");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/bin/php", ["--version"], ProcessResult.Ok(0, "PHP 8.2.12 (cli) (built: Oct 24 2023)\n", "", TimeSpan.Zero));

        var result = await new PhpDetector(locator, runner).DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("8.2.12", row.Version);
    }

    [Fact]
    public async Task PhpDetector_NotOnPath_ReturnsNotDetected()
    {
        var result = await new PhpDetector(new FakeExecutableLocator(), new FakeProcessRunner()).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task GoDetector_TypicalVersionAndEnvVars_AreRedacted()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("go", "/usr/local/go/bin/go");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/local/go/bin/go", ["version"], ProcessResult.Ok(0, "go version go1.21.5 linux/amd64\n", "", TimeSpan.Zero));

        Environment.SetEnvironmentVariable("GOROOT", "/usr/local/go");
        try
        {
            var result = await new GoDetector(locator, runner, new SecretRedactor()).DetectAsync(CancellationToken.None);

            var row = Assert.Single(result.Rows);
            Assert.Equal("1.21.5", row.Version);
            Assert.True(row.EnvironmentVariables.ContainsKey("GOROOT"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOROOT", null);
        }
    }

    [Fact]
    public async Task GoDetector_NotOnPath_ReturnsNotDetected()
    {
        var result = await new GoDetector(new FakeExecutableLocator(), new FakeProcessRunner(), new SecretRedactor()).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }
}
