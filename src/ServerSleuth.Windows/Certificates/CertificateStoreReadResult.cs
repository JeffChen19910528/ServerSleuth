using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Certificates;

public sealed record CertificateStoreReadResult
{
    public required OperationStatus Status { get; init; }
    public IReadOnlyList<CertificateRow> Certificates { get; init; } = [];

    public bool Success => Status == OperationStatus.Success;

    public static CertificateStoreReadResult Ok(IReadOnlyList<CertificateRow> certificates) =>
        new() { Status = OperationStatus.Success, Certificates = certificates };

    public static CertificateStoreReadResult Failure(OperationStatus status) => new() { Status = status };
}
