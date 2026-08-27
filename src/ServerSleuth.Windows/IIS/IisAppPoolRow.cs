namespace ServerSleuth.Windows.IIS;

public sealed record IisAppPoolRow
{
    public required string Name { get; init; }
    public required string State { get; init; }
    public string? ManagedRuntimeVersion { get; init; }
    public string? ManagedPipelineMode { get; init; }
    public required string IdentityType { get; init; } // "ApplicationPoolIdentity","LocalSystem","LocalService","NetworkService","SpecificUser"
    public string? UserName { get; init; } // only set when IdentityType == SpecificUser
    public bool Enable32BitAppOnWin64 { get; init; }
    public string? StartMode { get; init; }
}
