using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Packages;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Packages;

public class RpmPackageProviderTests
{
    private static readonly string[] Args = ["-qa", "--queryformat", "%{NAME}\t%{VERSION}-%{RELEASE}\t%{ARCH}\t%{VENDOR}\t%{SOURCERPM}\n"];

    [Fact]
    public async Task QueryInstalledPackagesAsync_TypicalOutput_ParsesAllFields()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("rpm", Args, ProcessResult.Ok(0, "httpd\t2.4.57-1.el9\tx86_64\tFedora Project\thttpd-2.4.57-1.el9.src.rpm\n", "", TimeSpan.Zero));

        var result = await new RpmPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        var pkg = Assert.Single(result.Packages);
        Assert.Equal("httpd", pkg.Name);
        Assert.Equal("2.4.57-1.el9", pkg.Version);
        Assert.Equal("x86_64", pkg.Architecture);
        Assert.Equal("httpd-2.4.57-1.el9.src.rpm", pkg.SourcePackage);
    }

    [Fact]
    public async Task QueryInstalledPackagesAsync_NoneVendor_IsTreatedAsNull()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("rpm", Args, ProcessResult.Ok(0, "somepkg\t1.0-1\tnoarch\t(none)\t(none)\n", "", TimeSpan.Zero));

        var result = await new RpmPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        var pkg = Assert.Single(result.Packages);
        Assert.Null(pkg.Maintainer);
        Assert.Null(pkg.SourcePackage);
    }

    [Fact]
    public async Task QueryInstalledPackagesAsync_MalformedLine_IsSkippedWithoutThrowing()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("rpm", Args, ProcessResult.Ok(0, "onlyonefield\n", "", TimeSpan.Zero));

        var result = await new RpmPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Empty(result.Packages);
    }

    [Fact]
    public async Task QueryInstalledPackagesAsync_RpmNotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner();

        var result = await new RpmPackageProvider(runner).QueryInstalledPackagesAsync(CancellationToken.None);

        Assert.Equal(PackageManagerAvailability.NotInstalled, result.Status);
    }
}
