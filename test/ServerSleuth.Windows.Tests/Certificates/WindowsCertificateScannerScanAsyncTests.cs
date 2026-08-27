using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Certificates;

namespace ServerSleuth.Windows.Tests.Certificates;

internal sealed class FakeCertificateStoreProvider(Dictionary<string, CertificateStoreReadResult> byLabel) : ICertificateStoreProvider
{
    public CertificateStoreReadResult ReadStore(CertificateStoreSource source) =>
        byLabel.GetValueOrDefault(source.Label, CertificateStoreReadResult.Ok([]));
}

public class WindowsCertificateScannerScanAsyncTests
{
    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None };

    private static CertificateRow MakeRow(string thumbprint) => new()
    {
        Thumbprint = thumbprint,
        Subject = "CN=Test",
        Issuer = "CN=Test CA",
        NotBefore = DateTimeOffset.UtcNow.AddYears(-1),
        NotAfter = DateTimeOffset.UtcNow.AddYears(1)
    };

    [Fact]
    public async Task ScanAsync_AllStoresAccessible_ReturnsSupportedWithAllCertificates()
    {
        var byLabel = new Dictionary<string, CertificateStoreReadResult>
        {
            [CertificateStoreSource.LocalMachineMy.Label] = CertificateStoreReadResult.Ok([MakeRow("AAA"), MakeRow("BBB")]),
            [CertificateStoreSource.CurrentUserMy.Label] = CertificateStoreReadResult.Ok([MakeRow("CCC")])
        };

        var scanner = new WindowsCertificateScanner(new FakeCertificateStoreProvider(byLabel), NullLogger<WindowsCertificateScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(3, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OneStoreAccessDenied_ReturnsPartiallySupportedKeepingOthers()
    {
        var byLabel = new Dictionary<string, CertificateStoreReadResult>
        {
            [CertificateStoreSource.LocalMachineMy.Label] = CertificateStoreReadResult.Ok([MakeRow("AAA")]),
            [CertificateStoreSource.LocalMachineWebHosting.Label] = CertificateStoreReadResult.Failure(OperationStatus.AccessDenied)
        };

        var scanner = new WindowsCertificateScanner(new FakeCertificateStoreProvider(byLabel), NullLogger<WindowsCertificateScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_AllStoresAccessDenied_ReturnsAccessDenied()
    {
        var byLabel = CertificateStoreSource.All.ToDictionary(s => s.Label, _ => CertificateStoreReadResult.Failure(OperationStatus.AccessDenied));

        var scanner = new WindowsCertificateScanner(new FakeCertificateStoreProvider(byLabel), NullLogger<WindowsCertificateScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.AccessDenied, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_NoCertificatesAnywhere_ReturnsSupportedNotAccessDenied()
    {
        var scanner = new WindowsCertificateScanner(new FakeCertificateStoreProvider(new Dictionary<string, CertificateStoreReadResult>()), NullLogger<WindowsCertificateScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
