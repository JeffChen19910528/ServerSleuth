using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Common;
using CoreCertificate = ServerSleuth.Core.Models.Certificate;

namespace ServerSleuth.Windows.Certificates;

/// <summary>
/// Discovers certificates in the targeted, application-relevant stores (LocalMachine\My,
/// LocalMachine\WebHosting, LocalMachine\Root, CurrentUser\My) — never every store on the
/// machine. Never exports or reads private key material; HasPrivateKey is a flag only.
/// See skill.md §15, §17.
/// </summary>
public sealed class WindowsCertificateScanner(ICertificateStoreProvider provider, ILogger<WindowsCertificateScanner> logger)
    : IDiscoveryScanner
{
    public string Id => "windows-certificate-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<CoreCertificate>();
        var deniedStores = 0;
        var asOf = DateTimeOffset.UtcNow;

        foreach (var source in CertificateStoreSource.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readResult = provider.ReadStore(source);
            if (!readResult.Success)
            {
                deniedStores++;
                logger.LogWarning(ScannerLogEvents.PermissionDenied, "{ScannerId} could not read {Store}: {Status}", Id, source.Label, readResult.Status);
                continue;
            }

            entities.AddRange(readResult.Certificates.Select(row => BuildEntity(row, source, asOf)));
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} certificates", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = deniedStores switch
        {
            var n when n == CertificateStoreSource.All.Count => ScannerStatus.AccessDenied,
            var n when n > 0 => ScannerStatus.PartiallySupported,
            _ => ScannerStatus.Supported
        };

        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities });
    }

    /// <summary>Pure mapping, unit-testable against a synthetic CertificateRow with a fixed
    /// "as of" time so tests never depend on today's date. See skill.md §22.</summary>
    internal static CoreCertificate BuildEntity(CertificateRow row, CertificateStoreSource source, DateTimeOffset asOf)
    {
        var normalizedThumbprint = NormalizeThumbprint(row.Thumbprint);

        var entity = new CoreCertificate
        {
            Id = $"cert:{source.Label}:{normalizedThumbprint}",
            Name = row.FriendlyName ?? row.Subject,
            Type = "Certificate",
            Source = EvidenceSources.WindowsCertificateStore,
            Status = EntityStatus.Installed,
            Confidence = Confidence.VeryHigh(),
            Subject = row.Subject,
            Issuer = row.Issuer,
            Thumbprint = normalizedThumbprint,
            ValidFrom = row.NotBefore,
            ValidTo = row.NotAfter,
            SubjectAlternativeNames = row.SubjectAlternativeNames
        };

        entity.AddEvidence(new EvidenceRecord
        {
            Type = EvidenceType.CertificateStore,
            Location = $@"{source.Label}\{normalizedThumbprint}"
        });

        entity.SetMetadata("Store", source.StoreName);
        entity.SetMetadata("StoreLocation", source.Location.ToString());
        entity.SetMetadata("HasPrivateKey", row.HasPrivateKey.ToString());
        entity.SetMetadata("CertificateStatus", CertificateExpiryClassifier.ClassifyStatus(row.NotBefore, row.NotAfter, asOf));
        entity.SetMetadata("RiskLevel", CertificateExpiryClassifier.ClassifyRiskLevel(row.NotAfter, asOf));

        if (row.SerialNumber is not null) entity.SetMetadata("SerialNumber", row.SerialNumber);
        if (row.SignatureAlgorithm is not null) entity.SetMetadata("SignatureAlgorithm", row.SignatureAlgorithm);
        if (row.PublicKeyAlgorithm is not null) entity.SetMetadata("PublicKeyAlgorithm", row.PublicKeyAlgorithm);
        if (row.KeySizeBits is not null) entity.SetMetadata("KeySizeBits", row.KeySizeBits.Value.ToString());
        if (row.FriendlyName is not null) entity.SetMetadata("FriendlyName", row.FriendlyName);

        return entity;
    }

    /// <summary>Uppercase, no whitespace — matches the format IIS's binding CertificateHash
    /// (Convert.ToHexString) and X509Certificate2.Thumbprint both already naturally produce,
    /// so a later correlation step (Phase 5) can join on Thumbprint reliably. See skill.md §19.</summary>
    private static string NormalizeThumbprint(string thumbprint) =>
        thumbprint.Replace(" ", string.Empty).Trim().ToUpperInvariant();
}
