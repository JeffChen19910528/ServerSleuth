using System.Security;
using Microsoft.Win32;
using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Registry;

public sealed class WindowsRegistryReader : IWindowsRegistryReader
{
    public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryHive hive, RegistryView view, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path);
            if (key is null)
            {
                return RegistryResult<IReadOnlyList<string>>.Failure(OperationStatus.NotFound);
            }

            IReadOnlyList<string> names = key.GetSubKeyNames();
            return RegistryResult<IReadOnlyList<string>>.Ok(names);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return RegistryResult<IReadOnlyList<string>>.Failure(status);
        }
    }

    public RegistryResult<IReadOnlyDictionary<string, object?>> GetValues(RegistryHive hive, RegistryView view, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path);
            if (key is null)
            {
                return RegistryResult<IReadOnlyDictionary<string, object?>>.Failure(OperationStatus.NotFound);
            }

            var values = key.GetValueNames().ToDictionary(name => name, key.GetValue);
            return RegistryResult<IReadOnlyDictionary<string, object?>>.Ok(values);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return RegistryResult<IReadOnlyDictionary<string, object?>>.Failure(status);
        }
    }

    public RegistryResult<object?> GetValue(RegistryHive hive, RegistryView view, string path, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path);
            if (key is null)
            {
                return RegistryResult<object?>.Failure(OperationStatus.NotFound);
            }

            return RegistryResult<object?>.Ok(key.GetValue(valueName));
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return RegistryResult<object?>.Failure(status);
        }
    }

    private static bool TryClassify(Exception ex, out OperationStatus status)
    {
        status = ex switch
        {
            UnauthorizedAccessException => OperationStatus.AccessDenied,
            SecurityException => OperationStatus.AccessDenied,
            _ => OperationStatus.IoError
        };

        return ex is UnauthorizedAccessException or SecurityException or IOException;
    }
}
