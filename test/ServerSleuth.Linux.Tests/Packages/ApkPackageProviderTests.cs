using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Packages;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Packages;

public class ApkPackageProviderTests
{
    private static readonly string[] Args = ["info", "-v"];

    [Fact]
    public async Task QueryInstalledPackagesAsync_TypicalOutput_ParsesEachLine()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("apk", Args, ProcessResult.Ok(0, "musl-1.2.4-r2\nopenssl-3.1.4-r3\n", "", TimeSpan.Zero));

        var result = await new ApkPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Equal(2, result.Packages.Count);
        Assert.Contains(result.Packages, p => p.Name == "musl" && p.Version == "1.2.4-r2");
    }

    [Fact]
    public async Task QueryInstalledPackagesAsync_ApkNotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner();

        var result = await new ApkPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Equal(PackageManagerAvailability.NotInstalled, result.Status);
    }
}
