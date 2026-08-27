using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>Outcome of <see cref="ISshSession.Connect"/> — reuses the shared
/// <see cref="OperationStatus"/> enum (skill.md Phase 10D-2 §9: "Map: authentication failure →
/// AccessDenied or TransportUnavailable... connection failure → TransportUnavailable").</summary>
public sealed record SshConnectResult
{
    public required OperationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public bool Success => Status == OperationStatus.Success;

    public static SshConnectResult Ok() => new() { Status = OperationStatus.Success };

    public static SshConnectResult HostKeyRejected() => new()
    {
        Status = OperationStatus.TransportUnavailable,
        ErrorMessage = "The remote host's SSH key is not trusted."
    };

    public static SshConnectResult AuthenticationFailed() => new()
    {
        Status = OperationStatus.AccessDenied,
        ErrorMessage = "SSH authentication was rejected by the remote host."
    };

    public static SshConnectResult Unreachable(string errorMessage) => new()
    {
        Status = OperationStatus.TransportUnavailable,
        ErrorMessage = errorMessage
    };

    public static SshConnectResult TimedOut() => new()
    {
        Status = OperationStatus.Timeout,
        ErrorMessage = "Timed out establishing the SSH connection."
    };

    public static SshConnectResult Cancelled() => new() { Status = OperationStatus.Cancelled };
}
