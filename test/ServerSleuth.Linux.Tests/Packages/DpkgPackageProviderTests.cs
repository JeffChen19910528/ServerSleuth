using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Packages;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Packages;

public class DpkgPackageProviderTests
{
    private static readonly string[] Args = ["-W", "-f=${Package}\t${Version}\t${Architecture}\t${Maintainer}\n"];

    [Fact]
    public async Task QueryInstalledPackagesAsync_TypicalOutput_ParsesAllFields()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("dpkg-query", Args, ProcessResult.Ok(0, "bash\t5.1-6ubuntu1\tamd64\tUbuntu Developers <ubuntu-devel-discuss@lists.ubuntu.com>\n", "", TimeSpan.Zero));

        var result = await new DpkgPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Equal(PackageManagerAvailability.Available, result.Status);
        var pkg = Assert.Single(result.Packages);
        Assert.Equal("bash", pkg.Name);
        Assert.Equal("5.1-6ubuntu1", pkg.Version);
        Assert.Equal("amd64", pkg.Architecture);
    }

    [Fact]
    public async Task QueryInstalledPackagesAsync_MalformedLine_IsSkippedWithoutThrowing()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("dpkg-query", Args, ProcessResult.Ok(0, "bash\t5.1-6ubuntu1\tamd64\tMaintainer\nmalformed-line-no-tabs\n", "", TimeSpan.Zero));

        var result = await new DpkgPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Single(result.Packages);
    }

    [Fact]
    public async Task QueryInstalledPackagesAsync_DpkgQueryNotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner(); // dpkg-query not registered -> StartFailedResult

        var result = await new DpkgPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Equal(PackageManagerAvailability.NotInstalled, result.Status);
    }
}
