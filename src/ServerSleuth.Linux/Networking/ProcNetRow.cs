namespace ServerSleuth.Linux.Networking;

public sealed record ProcNetRow
{
    public required string LocalAddress { get; init; }
    public required int LocalPort { get; init; }
    public required string StateHex { get; init; }
    public required string Inode { get; init; }
}
