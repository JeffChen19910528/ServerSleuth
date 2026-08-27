using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Linux.Packages;

namespace ServerSleuth.Linux.Tests.Packages;

public class LinuxPackageScannerTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    private sealed class FakeProvider(string name, PackageQueryResult result) : IPackageManagerProvider
    {
        public string PackageManagerName => name;
        public Task<PackageQueryResult> QueryInstalledPackagesAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }

    [Fact]
    public void BuildEntity_MapsAllFieldsAndEvidence()
    {
        var row = new PackageRow { Name = "bash", Version = "5.1-6", Architecture = "amd64", Maintainer = "Ubuntu Developers", SourcePackage = "bash" };

        var entity = LinuxPackageScanner.BuildEntity(row, "dpkg");

        Assert.Equal("package:dpkg:bash:5.1-6:amd64", entity.Id);
        Assert.Equal("Ubuntu Developers", entity.Publisher);
        Assert.Equal("amd64", entity.Metadata["Architecture"]);
        Assert.Equal("bash", entity.Metadata["SourcePackage"]);
        Assert.Single(entity.Evidence);
        Assert.Equal(EvidenceType.PackageManager, entity.Evidence[0].Type);
    }

    [Fact]
    public async Task ScanAsync_OnlyDpkgAvailable_OthersNotInstalled_ReturnsSupported()
    {
        var dpkg = new FakeProvider("dpkg", new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = [new PackageRow { Name = "bash", Version = "5.1", Architecture = "amd64" }] });
        var rpm = new FakeProvider("rpm", new PackageQueryResult { Status = PackageManagerAvailability.NotInstalled });
        var apk = new FakeProvider("apk", new PackageQueryResult { Status = PackageManagerAvailability.NotInstalled });

        var scanner = new LinuxPackageScanner([dpkg, rpm, apk], NullLogger<LinuxPackageScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_DuplicatePackageWithinSameManager_ProducesOneEntity()
    {
        var row = new PackageRow { Name = "bash", Version = "5.1", Architecture = "amd64" };
        var dpkg = new FakeProvider("dpkg", new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = [row, row] });

        var scanner = new LinuxPackageScanner([dpkg], NullLogger<LinuxPackageScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_SameNameDifferentManagers_NeverMerged()
    {
        var dpkgRow = new PackageRow { Name = "openssl", Version = "3.0.2-0ubuntu1", Architecture = "amd64" };
        var rpmRow = new PackageRow { Name = "openssl", Version = "3.1.4-1.el9", Architecture = "x86_64" };

        var dpkg = new FakeProvider("dpkg", new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = [dpkgRow] });
        var rpm = new FakeProvider("rpm", new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = [rpmRow] });

        var scanner = new LinuxPackageScanner([dpkg, rpm], NullLogger<LinuxPackageScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(2, result.Entities.Count);
        Assert.Contains(result.Entities, e => e.Id.StartsWith("package:dpkg:"));
        Assert.Contains(result.Entities, e => e.Id.StartsWith("package:rpm:"));
    }

    [Fact]
    public async Task ScanAsync_ProviderAccessDenied_ReturnsPartiallySupported_NeverCrashes()
    {
        var dpkg = new FakeProvider("dpkg", new PackageQueryResult { Status = PackageManagerAvailability.AccessDenied, ErrorMessage = "denied" });

        var scanner = new LinuxPackageScanner([dpkg], NullLogger<LinuxPackageScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_NoPackageManagersAvailable_ReturnsNotInstalled()
    {
        var dpkg = new FakeProvider("dpkg", new PackageQueryResult { Status = PackageManagerAvailability.NotInstalled });
        var rpm = new FakeProvider("rpm", new PackageQueryResult { Status = PackageManagerAvailability.NotInstalled });
        var apk = new FakeProvider("apk", new PackageQueryResult { Status = PackageManagerAvailability.NotInstalled });

        var scanner = new LinuxPackageScanner([dpkg, rpm, apk], NullLogger<LinuxPackageScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
        Assert.Empty(result.Entities);
    }
}
