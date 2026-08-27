namespace ServerSleuth.Windows.COM;

public sealed record ComClsidReadResult
{
    public ComClsidRow? Row { get; init; }
    public string? FailureReason { get; init; }

    public bool Success => Row is not null;

    public static ComClsidReadResult Ok(ComClsidRow row) => new() { Row = row };
    public static ComClsidReadResult Failure(string reason) => new() { FailureReason = reason };
}
