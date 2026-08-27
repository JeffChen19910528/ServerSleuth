using Microsoft.Win32;

namespace ServerSleuth.Windows.Registry;

/// <summary>
/// Satisfies the SAME <see cref="IWindowsRegistryReader"/> interface every registry-reading
/// scanner/provider already depends on (<c>ComClsidReader</c>, <c>ServiceRegistryDetailReader</c>,
/// <c>SoftwareRegistryRowBuilder</c>) — zero scanner/provider code change needed, exactly
/// mirroring Phase 10D-2's "SshFileSystemReader satisfies IFileSystemReader" pattern (skill.md
/// Phase 10D-3B §2, §20). Backed by a <see cref="WinRmRegistryOperations"/> instance (itself
/// backed by the shared WinRM transport) — this class only translates between the three
/// existing local method shapes and the one <see cref="WindowsRegistryQuery"/> shape.
/// </summary>
public sealed class WinRmWindowsRegistryReader(WinRmRegistryOperations remoteRegistry) : IWindowsRegistryReader
{
    public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryHive hive, RegistryView view, string path)
    {
        var result = remoteRegistry.Query(WindowsRegistryQuery.ForSubKeyNames(hive, view, path));
        return result.Success
            ? RegistryResult<IReadOnlyList<string>>.Ok(result.Value!.SubKeyNames)
            : RegistryResult<IReadOnlyList<string>>.Failure(result.Status);
    }

    public RegistryResult<IReadOnlyDictionary<string, object?>> GetValues(RegistryHive hive, RegistryView view, string path)
    {
        var result = remoteRegistry.Query(WindowsRegistryQuery.ForAllValues(hive, view, path));
        return result.Success
            ? RegistryResult<IReadOnlyDictionary<string, object?>>.Ok(result.Value!.Values)
            : RegistryResult<IReadOnlyDictionary<string, object?>>.Failure(result.Status);
    }

    public RegistryResult<object?> GetValue(RegistryHive hive, RegistryView view, string path, string valueName)
    {
        var result = remoteRegistry.Query(WindowsRegistryQuery.ForOneValue(hive, view, path, valueName));
        if (!result.Success)
        {
            return RegistryResult<object?>.Failure(result.Status);
        }

        return result.Value!.Values.TryGetValue(valueName, out var value)
            ? RegistryResult<object?>.Ok(value)
            : RegistryResult<object?>.Failure(ServerSleuth.Infrastructure.Common.OperationStatus.NotFound);
    }
}
