namespace ServerSleuth.Windows.Certificates;

/// <summary>
/// Satisfies the SAME <see cref="ICertificateStoreProvider"/> interface
/// <see cref="WindowsCertificateScanner"/> already depends on — thin adapter over the
/// disclosed-gap <see cref="WinRmCertificateOperations"/> (see that type's own doc comment).
/// Always returns <see cref="CertificateStoreReadResult.Failure"/> — never fabricated
/// certificate data, never private-key material, never a local fallback.
/// </summary>
public sealed class WinRmCertificateStoreProvider(WinRmCertificateOperations remoteCertificates) : ICertificateStoreProvider
{
    public CertificateStoreReadResult ReadStore(CertificateStoreSource source)
    {
        var result = remoteCertificates.Query(source);
        return CertificateStoreReadResult.Failure(result.Status);
    }
}
