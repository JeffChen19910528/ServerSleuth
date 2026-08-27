using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The outcome of a single Windows remote capability query — the Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Targets.RemoteOperationResult"/>, and shaped like
/// <see cref="ServerSleuth.Windows.Registry.RegistryResult{T}"/> (this project's own existing
/// "typed result, shared <see cref="OperationStatus"/>, never an exception for an expected
/// failure mode" convention) rather than introducing a third, differently-shaped result type.
/// Reused across all five <see cref="WindowsRemoteOperationKind"/> members instead of one
/// result type per kind, since the shape (status + optional typed value + optional error +
/// duration) is identical for all of them.
///
/// Carries no credential field of any kind (skill.md §13, §16) — nothing in this phase's
/// capability model has anywhere to put one, since none of the five query types accepts or
/// returns authentication material. Nothing in this codebase produces a real instance of this
/// type yet — no transport executes a Windows remote capability query in this phase (skill.md
/// §3, §27: model only, no WinRM).
/// </summary>
public sealed record WindowsRemoteOperationResult<T>
{
    public required OperationStatus Status { get; init; }
    public T? Value { get; init; }

    /// <summary>Set for a non-<see cref="OperationStatus.Success"/> outcome — never a raw
    /// secret value, matching the same convention
    /// <see cref="ServerSleuth.Infrastructure.Targets.RemoteOperation"/>'s doc comment already
    /// establishes for the cross-platform result type.</summary>
    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }

    public bool Success => Status == OperationStatus.Success;

    public static WindowsRemoteOperationResult<T> Ok(T value, TimeSpan duration = default) => new()
    {
        Status = OperationStatus.Success,
        Value = value,
        Duration = duration
    };

    public static WindowsRemoteOperationResult<T> Failure(OperationStatus status, string? errorMessage = null, TimeSpan duration = default) => new()
    {
        Status = status,
        ErrorMessage = errorMessage,
        Duration = duration
    };
}
