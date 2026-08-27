using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Certificates;

/// <summary>
/// The capability boundary a future WinRM transport must satisfy to serve certificate-store
/// metadata — see skill.md (Phase 10D-3A) §11, §14. Takes the SAME
/// <see cref="CertificateStoreSource"/> the local <see cref="ICertificateStoreProvider"/>
/// already takes and returns the SAME <see cref="CertificateRow"/> shape — both reused directly
/// rather than duplicated, since <see cref="CertificateRow"/> already satisfies every field
/// skill.md §11 asks for (store/thumbprint/subject/issuer/validity/<see cref="CertificateRow.HasPrivateKey"/>).
///
/// <see cref="CertificateRow.HasPrivateKey"/> is, and remains, a boolean flag — nothing in this
/// interface, this query type, or this result type has anywhere to put actual private-key
/// bytes: there is no <c>ExportPrivateKey</c> method, no <c>byte[]</c>-returning member, and no
/// field a future implementation could misuse to smuggle key material through (skill.md §11's
/// "never expose private key material" made structurally impossible, not just documented).
///
/// No implementation of this interface exists anywhere in this codebase yet (skill.md §3, §18,
/// §27: model only).
/// </summary>
public interface IWindowsRemoteCertificateOperations
{
    ScanTarget Target { get; }

    WindowsRemoteOperationResult<IReadOnlyList<CertificateRow>> Query(CertificateStoreSource source);
}
