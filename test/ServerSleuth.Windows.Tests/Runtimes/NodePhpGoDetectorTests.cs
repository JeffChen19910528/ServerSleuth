using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Runtimes;

public class NodePhpGoDetectorTests
{
    private const string NodePath = @"C:\Program Files\nodejs\node.exe";
    private const string NpmPath = @"C:\Program Files\nodejs\npm.cmd";

    [Fact]
    public async Task NodeDetector_BothNodeAndNpmPresent_ProducesTwoRows()
    {
        var locator = new FakeExecutableLocator(new() { ["node.exe"] = NodePath, ["npm.cmd"] = NpmPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{NodePath}|--version"] = ProcessResult.Ok(0, "v22.14.0", string.Empty, TimeSpan.Zero),
            [$"{NpmPath}|--version"] = ProcessResult.Ok(0, "10.9.2", string.Empty, TimeSpan.Zero)
        });

        var detector = new NodeDetector(locator, runner);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Name == "Node.js" && r.Version == "22.14.0"); // leading 'v' stripped
        Assert.Contains(result.Rows, r => r.Name == "npm" && r.Version == "10.9.2");
    }

    [Fact]
    public async Task NodeDetector_OnlyNodePresent_ProducesOneRow()
    {
        var locator = new FakeExecutableLocator(new() { ["node.exe"] = NodePath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{NodePath}|--version"] = ProcessResult.Ok(0, "v22.14.0", string.Empty, TimeSpan.Zero)
        });

        var detector = new NodeDetector(locator, runner);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Single(result.Rows);
    }

    [Fact]
    public async Task NodeDetector_NeitherPresent_ReturnsNotDetected()
    {
        var detector = new NodeDetector(new FakeExecutableLocator(new()), new FakeProcessRunner(new()));
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task PhpDetector_ParsesVersionFromCliOutput()
    {
        const string phpPath = @"C:\php\php.exe";
        var locator = new FakeExecutableLocator(new() { ["php.exe"] = phpPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{phpPath}|--version"] = ProcessResult.Ok(0, "PHP 8.3.6 (cli) (built: Apr 10 2024)", string.Empty, TimeSpan.Zero)
        });

        var detector = new PhpDetector(locator, runner);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal("8.3.6", Assert.Single(result.Rows).Version);
    }

    [Fact]
    public async Task PhpDetector_NotFound_ReturnsNotDetected()
    {
        var detector = new PhpDetector(new FakeExecutableLocator(new()), new FakeProcessRunner(new()));
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task GoDetector_ParsesVersionAndRedactsEnvironmentVariables()
    {
        const string goPath = @"C:\Go\bin\go.exe";
        var locator = new FakeExecutableLocator(new() { ["go.exe"] = goPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{goPath}|version"] = ProcessResult.Ok(0, "go version go1.23.4 windows/amd64", string.Empty, TimeSpan.Zero)
        });

        Environment.SetEnvironmentVariable("GOROOT", @"C:\Go");
        try
        {
            var detector = new GoDetector(locator, runner, new SecretRedactor());
            var result = await detector.DetectAsync(CancellationToken.None);

            var row = Assert.Single(result.Rows);
            Assert.Equal("1.23.4", row.Version);
            Assert.Equal(@"C:\Go", row.EnvironmentVariables["GOROOT"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GOROOT", null);
        }
    }

    [Fact]
    public async Task GoDetector_NotFound_ReturnsNotDetected()
    {
        var detector = new GoDetector(new FakeExecutableLocator(new()), new FakeProcessRunner(new()), new SecretRedactor());
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }
}
