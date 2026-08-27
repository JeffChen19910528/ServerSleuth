using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Linux.Runtimes.Detectors;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Runtimes;

public class DotNetRuntimeAndSdkDetectorTests
{
    [Fact]
    public async Task DotNetRuntimeDetector_MultipleRuntimeVersions_ProducesOneRowPerVersion()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("dotnet", "/usr/share/dotnet/dotnet");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/share/dotnet/dotnet", ["--list-runtimes"], ProcessResult.Ok(0,
            "Microsoft.NETCore.App 6.0.25 [/usr/share/dotnet/shared/Microsoft.NETCore.App]\n" +
            "Microsoft.NETCore.App 8.0.10 [/usr/share/dotnet/shared/Microsoft.NETCore.App]\n" +
            "Microsoft.NETCore.App 10.0.0 [/usr/share/dotnet/shared/Microsoft.NETCore.App]\n", "", TimeSpan.Zero));

        var result = await new DotNetRuntimeDetector(locator, runner).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(3, result.Rows.Count); // never collapsed
        Assert.Contains(result.Rows, r => r.Version == "6.0.25");
        Assert.Contains(result.Rows, r => r.Version == "8.0.10");
        Assert.Contains(result.Rows, r => r.Version == "10.0.0");
    }

    [Fact]
    public async Task DotNetRuntimeDetector_DotnetNotOnPath_ReturnsNotDetected()
    {
        var result = await new DotNetRuntimeDetector(new FakeExecutableLocator(), new FakeProcessRunner()).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task DotNetSdkDetector_TwoSdkVersions_ProducesSdkEntityKind()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("dotnet", "/usr/share/dotnet/dotnet");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/share/dotnet/dotnet", ["--list-sdks"], ProcessResult.Ok(0,
            "8.0.400 [/usr/share/dotnet/sdk]\n10.0.100 [/usr/share/dotnet/sdk]\n", "", TimeSpan.Zero));

        var result = await new DotNetSdkDetector(locator, runner).DetectAsync(CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal(RuntimeEntityKind.Sdk, r.EntityKind));
        Assert.Contains(result.Rows, r => r.Version == "8.0.400");
    }

    [Fact]
    public async Task DotNetRuntimeDetector_CommandTimesOutOrFails_ReturnsPartial()
    {
        var locator = new FakeExecutableLocator();
        locator.SetPath("dotnet", "/usr/share/dotnet/dotnet");
        var runner = new FakeProcessRunner();
        runner.SetResult("/usr/share/dotnet/dotnet", ["--list-runtimes"], ProcessResult.TimedOutResult(TimeSpan.FromSeconds(10)));

        var result = await new DotNetRuntimeDetector(locator, runner).DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
    }
}
