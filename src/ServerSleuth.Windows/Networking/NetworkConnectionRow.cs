namespace ServerSleuth.Windows.Networking;

public sealed record NetworkConnectionRow
{
    public required string LocalAddress { get; init; }
    public required int LocalPort { get; init; }
    public int? OwningProcessId { get; init; }
}
