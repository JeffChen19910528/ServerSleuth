namespace ServerSleuth.Windows.Certificates;

/// <summary>Raw, public-only certificate metadata. Never carries private key material —
/// HasPrivateKey is a boolean flag, not the key itself. See skill.md §17.</summary>
public sealed record CertificateRow
{
    public required string Thumbprint { get; init; }
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
    public string? SerialNumber { get; init; }
    public required DateTimeOffset NotBefore { get; init; }
    public required DateTimeOffset NotAfter { get; init; }
    public bool HasPrivateKey { get; init; }
    public string? SignatureAlgorithm { get; init; }
    public string? PublicKeyAlgorithm { get; init; }
    public int? KeySizeBits { get; init; }
    public IReadOnlyList<string> SubjectAlternativeNames { get; init; } = [];
    public string? FriendlyName { get; init; }
}
