using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Certificates;

/// <summary>
/// Reads certificates via the standard .NET X509Store API — read-only (OpenFlags.ReadOnly),
/// never exports or accesses private key bytes. See skill.md §17, §24.
/// </summary>
public sealed class CertificateStoreProvider(ILogger<CertificateStoreProvider> logger) : ICertificateStoreProvider
{
    public CertificateStoreReadResult ReadStore(CertificateStoreSource source)
    {
        try
        {
            using var store = new X509Store(source.StoreName, source.Location);
            store.Open(OpenFlags.ReadOnly);

            var rows = new List<CertificateRow>();
            foreach (var certificate in store.Certificates)
            {
                using (certificate)
                {
                    rows.Add(ReadCertificate(certificate));
                }
            }

            return CertificateStoreReadResult.Ok(rows);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Failed to open certificate store {Store}", source.Label);
            return CertificateStoreReadResult.Failure(OperationStatus.AccessDenied);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read certificate store {Store}", source.Label);
            return CertificateStoreReadResult.Failure(OperationStatus.IoError);
        }
    }

    private static CertificateRow ReadCertificate(X509Certificate2 certificate)
    {
        return new CertificateRow
        {
            Thumbprint = certificate.Thumbprint,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            SerialNumber = certificate.SerialNumber,
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
            HasPrivateKey = certificate.HasPrivateKey,
            SignatureAlgorithm = certificate.SignatureAlgorithm?.FriendlyName,
            PublicKeyAlgorithm = certificate.PublicKey?.Oid?.FriendlyName,
            KeySizeBits = TryGetKeySize(certificate),
            SubjectAlternativeNames = ReadSubjectAlternativeNames(certificate),
            FriendlyName = string.IsNullOrWhiteSpace(certificate.FriendlyName) ? null : certificate.FriendlyName
        };
    }

    private static int? TryGetKeySize(X509Certificate2 certificate)
    {
        try
        {
            return certificate.GetRSAPublicKey()?.KeySize
                   ?? certificate.GetECDsaPublicKey()?.KeySize
                   ?? certificate.GetDSAPublicKey()?.KeySize;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadSubjectAlternativeNames(X509Certificate2 certificate)
    {
        var sanExtension = certificate.Extensions["2.5.29.17"];
        if (sanExtension is null)
        {
            return [];
        }

        try
        {
            var formatted = sanExtension.Format(false);
            return formatted
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
