using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Certificates;

/// <summary>
/// A DISCLOSED capability gap (skill.md Phase 10D-3B §15, §34) — the LOCAL
/// <see cref="CertificateStoreProvider"/> uses <c>System.Security.Cryptography.X509Certificates.X509Store</c>,
/// which is local-machine-only with no WS-Man/WMI-reachable equivalent. Certificate BLOBs are
/// technically present under a registry path
/// (<c>HKLM\SOFTWARE\Microsoft\SystemCertificates\...\Certificates\&lt;thumbprint&gt;\Blob</c>),
/// reachable in principle via <see cref="Registry.IWindowsRemoteRegistryOperations"/>, but this
/// phase does NOT attempt to parse that undocumented, internal, serialized-store binary format
/// remotely: getting the parse wrong risks silently wrong thumbprint/subject/issuer data, which
/// is worse than reporting nothing — and skill.md itself instructs stopping rather than
/// guessing at an unverified format. Every call returns a <see cref="OperationStatus.NotInstalled"/>
/// result with a diagnostic — never fabricated certificate data, and structurally incapable of
/// exporting a private key (no such field/method exists anywhere in this type or its interface).
/// </summary>
public sealed class WinRmCertificateOperations(ScanTarget target) : IWindowsRemoteCertificateOperations
{
    public ScanTarget Target { get; } = target;

    public WindowsRemoteOperationResult<IReadOnlyList<CertificateRow>> Query(CertificateStoreSource source) =>
        WindowsRemoteOperationResult<IReadOnlyList<CertificateRow>>.Failure(
            OperationStatus.NotInstalled,
            $"Remote certificate-store discovery ('{source.Label}') is not implemented in Phase 10D-3B: X509Store " +
            "is local-only, and this phase does not attempt to parse the undocumented registry-stored certificate " +
            "blob format remotely. Disclosed gap — see ARCHITECTURE.md's Phase 10D-3B addendum.");
}
