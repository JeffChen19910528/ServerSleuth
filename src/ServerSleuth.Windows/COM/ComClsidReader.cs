using Microsoft.Win32;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Registry;

namespace ServerSleuth.Windows.COM;

/// <summary>
/// Reads one CLSID's registration detail. Only reads the child keys skill.md §7's field list
/// actually needs (InprocServer32, LocalServer32, ProgID, TypeLib, Version) — and only those
/// that are actually present, per the child-key list read first — never an unbounded
/// recursive traversal. See skill.md §25 (performance) and §3 (only read necessary keys).
/// </summary>
public static class ComClsidReader
{
    public static ComClsidReadResult Read(IWindowsRegistryReader registryReader, RegistryHive hive, RegistryView view, string clsidRootPath, string clsid)
    {
        var clsidKeyPath = $@"{clsidRootPath}\{clsid}";

        var ownValues = registryReader.GetValues(hive, view, clsidKeyPath);
        if (ownValues.Status == OperationStatus.AccessDenied)
        {
            return ComClsidReadResult.Failure($"CLSID '{clsid}': access denied");
        }

        var childNamesResult = registryReader.GetSubKeyNames(hive, view, clsidKeyPath);
        if (childNamesResult.Status == OperationStatus.AccessDenied)
        {
            return ComClsidReadResult.Failure($"CLSID '{clsid}': access denied enumerating subkeys");
        }

        var childNames = childNamesResult.Success ? childNamesResult.Value! : (IReadOnlyList<string>)[];

        var row = new ComClsidRow
        {
            Clsid = clsid,
            Name = ownValues.Success ? ownValues.Value!.GetValueOrDefault(string.Empty) as string : null,
            ProgId = ReadDefaultValue(registryReader, hive, view, clsidKeyPath, childNames, "ProgID"),
            InprocServer32 = ReadServerReference(registryReader, hive, view, clsidKeyPath, childNames, "InprocServer32", out var threadingModel),
            ThreadingModel = threadingModel,
            LocalServer32 = ReadServerReference(registryReader, hive, view, clsidKeyPath, childNames, "LocalServer32", out _),
            TypeLibClsid = ReadDefaultValue(registryReader, hive, view, clsidKeyPath, childNames, "TypeLib"),
            VersionValue = ReadDefaultValue(registryReader, hive, view, clsidKeyPath, childNames, "Version")
        };

        return ComClsidReadResult.Ok(row);
    }

    private static string? ReadDefaultValue(
        IWindowsRegistryReader registryReader, RegistryHive hive, RegistryView view,
        string clsidKeyPath, IReadOnlyList<string> childNames, string childKeyName)
    {
        if (!childNames.Contains(childKeyName, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var values = registryReader.GetValues(hive, view, $@"{clsidKeyPath}\{childKeyName}");
        return values.Success ? values.Value!.GetValueOrDefault(string.Empty) as string : null;
    }

    private static ServerReference? ReadServerReference(
        IWindowsRegistryReader registryReader, RegistryHive hive, RegistryView view,
        string clsidKeyPath, IReadOnlyList<string> childNames, string childKeyName, out string? threadingModel)
    {
        threadingModel = null;

        if (!childNames.Contains(childKeyName, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var values = registryReader.GetValues(hive, view, $@"{clsidKeyPath}\{childKeyName}");
        if (!values.Success)
        {
            return null;
        }

        threadingModel = values.Value!.GetValueOrDefault("ThreadingModel") as string;
        var rawValue = values.Value!.GetValueOrDefault(string.Empty) as string;

        return rawValue is not null ? ServerReference.Parse(rawValue) : null;
    }
}
