using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Infrastructure.FileSystem;

/// <summary>
/// Wraps the outcome of a single filesystem operation so a permission/IO failure surfaces
/// as data rather than an unhandled exception that would abort the scan. See skill.md §25-26.
/// </summary>
public sealed record FileSystemResult<T>
{
    public required OperationStatus Status { get; init; }
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }

    public bool Success => Status == OperationStatus.Success;

    public static FileSystemResult<T> Ok(T value) => new() { Status = OperationStatus.Success, Value = value };

    public static FileSystemResult<T> Failure(OperationStatus status, string errorMessage) =>
        new() { Status = status, ErrorMessage = errorMessage };
}
