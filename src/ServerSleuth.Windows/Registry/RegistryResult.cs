using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Registry;

public sealed record RegistryResult<T>
{
    public required OperationStatus Status { get; init; }
    public T? Value { get; init; }

    public bool Success => Status == OperationStatus.Success;

    public static RegistryResult<T> Ok(T value) => new() { Status = OperationStatus.Success, Value = value };
    public static RegistryResult<T> Failure(OperationStatus status) => new() { Status = status };
}
