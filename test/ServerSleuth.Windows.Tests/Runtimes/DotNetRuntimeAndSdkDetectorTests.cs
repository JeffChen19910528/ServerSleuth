using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Runtimes;

public class DotNetRuntimeAndSdkDetectorTests
{
    private const string DotnetPath = @"C:\Program Files\dotnet\dotnet.exe";

    [Fact]
    public async Task RuntimeDetector_DotnetNotFound_ReturnsNotDetected()
    {
        var detector = new DotNetRuntimeDetector(new FakeExecutableLocator(new()), new FakeProcessRunner(new()));

        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task RuntimeDetector_MultipleLines_ProducesOneRowPerLine()
    {
        var output = "Microsoft.AspNetCore.App 8.0.11 [C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App]\n" +
                     "Microsoft.NETCore.App 8.0.11 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]\n" +
                     "Microsoft.NETCore.App 6.0.35 [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]";

        var locator = new FakeExecutableLocator(new() { ["dotnet.exe"] = DotnetPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{DotnetPath}|--list-runtimes"] = ProcessResult.Ok(0, output, string.Empty, TimeSpan.Zero)
        });

        var detector = new DotNetRuntimeDetector(locator, runner);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Name == "Microsoft.NETCore.App" && r.Version == "6.0.35");
        Assert.Contains(result.Rows, r => r.Name == "Microsoft.NETCore.App" && r.Version == "8.0.11");
    }

    [Fact]
    public async Task RuntimeDetector_CommandFails_ReturnsPartial()
    {
        var locator = new FakeExecutableLocator(new() { ["dotnet.exe"] = DotnetPath });
        var runner = new FakeProcessRunner(new()); // no scripted response -> StartFailed

        var detector = new DotNetRuntimeDetector(locator, runner);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
    }

    [Fact]
    public async Task SdkDetector_MultipleSdks_AreReportedAsSdkEntitiesNotRuntimes()
    {
        var output = "6.0.428 [C:\\Program Files\\dotnet\\sdk]\n8.0.400 [C:\\Program Files\\dotnet\\sdk]";
        var locator = new FakeExecutableLocator(new() { ["dotnet.exe"] = DotnetPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{DotnetPath}|--list-sdks"] = ProcessResult.Ok(0, output, string.Empty, TimeSpan.Zero)
        });

        var detector = new DotNetSdkDetector(locator, runner);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal(RuntimeEntityKind.Sdk, r.EntityKind));
        Assert.Contains(result.Rows, r => r.Version == "8.0.400");
    }
}
