using Microsoft.Win32;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Registry;

namespace ServerSleuth.Windows.Tests.Fakes;

/// <summary>In-memory IWindowsRegistryReader for pure/fixture tests — no real registry access.</summary>
internal sealed class FakeWindowsRegistryReader : IWindowsRegistryReader
{
    private readonly Dictionary<string, IReadOnlyList<string>> _subKeyNames = new();
    private readonly Dictionary<string, IReadOnlyDictionary<string, object?>> _values = new();
    private readonly HashSet<string> _accessDeniedKeys = new();

    public void SetSubKeyNames(RegistryHive hive, RegistryView view, string path, params string[] names) =>
        _subKeyNames[Key(hive, view, path)] = names;

    public void SetValues(RegistryHive hive, RegistryView view, string path, IReadOnlyDictionary<string, object?> values) =>
        _values[Key(hive, view, path)] = values;

    public void SetAccessDenied(RegistryHive hive, RegistryView view, string path) =>
        _accessDeniedKeys.Add(Key(hive, view, path));

    public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryHive hive, RegistryView view, string path)
    {
        var key = Key(hive, view, path);
        if (_accessDeniedKeys.Contains(key))
        {
            return RegistryResult<IReadOnlyList<string>>.Failure(OperationStatus.AccessDenied);
        }

        return _subKeyNames.TryGetValue(key, out var names)
            ? RegistryResult<IReadOnlyList<string>>.Ok(names)
            : RegistryResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound);
    }

    public RegistryResult<IReadOnlyDictionary<string, object?>> GetValues(RegistryHive hive, RegistryView view, string path)
    {
        var key = Key(hive, view, path);
        if (_accessDeniedKeys.Contains(key))
        {
            return RegistryResult<IReadOnlyDictionary<string, object?>>.Failure(OperationStatus.AccessDenied);
        }

        return _values.TryGetValue(key, out var values)
            ? RegistryResult<IReadOnlyDictionary<string, object?>>.Ok(values)
            : RegistryResult<IReadOnlyDictionary<string, object?>>.Failure(OperationStatus.NotFound);
    }

    public RegistryResult<object?> GetValue(RegistryHive hive, RegistryView view, string path, string valueName)
    {
        var result = GetValues(hive, view, path);
        if (!result.Success)
        {
            return RegistryResult<object?>.Failure(result.Status);
        }

        return result.Value!.TryGetValue(valueName, out var value)
            ? RegistryResult<object?>.Ok(value)
            : RegistryResult<object?>.Ok(null);
    }

    private static string Key(RegistryHive hive, RegistryView view, string path) => $"{hive}|{view}|{path}";
}
