using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Remote;

/// <summary>The outcome of one <see cref="WindowsRemoteTargetTransport.Connect"/> attempt — the
/// Windows-domain counterpart to <see cref="ServerSleuth.Infrastructure.Remote.SshConnectResult"/>.</summary>
public sealed record WinRmConnectResult
{
    public required OperationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public bool Success => Status == OperationStatus.Success;

    public static WinRmConnectResult Ok() => new() { Status = OperationStatus.Success };
    public static WinRmConnectResult Failure(OperationStatus status, string errorMessage) => new() { Status = status, ErrorMessage = errorMessage };
}
