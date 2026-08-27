using Microsoft.Win32;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Registry;

/// <summary>
/// The real <see cref="IWindowsRemoteRegistryOperations"/> implementation — reads the remote
/// registry through the well-known <c>StdRegProv</c> WMI class's read-only methods (see
/// <see cref="WindowsWmiMethodAllowList"/>), invoked over the shared <see cref="CimWinRmTransport"/>.
/// This is the ONE structured, Microsoft-documented, WS-Man-reachable way to read a remote
/// Windows registry without PowerShell script text or the legacy Remote Registry (RPC/port 445)
/// service, which is a different, non-WinRM transport entirely.
///
/// **Registry32 is a disclosed, honest gap, not a silent wrong answer (skill.md §9, §21).**
/// <c>StdRegProv</c> has no WOW6432Node-redirection parameter — unlike the local
/// <c>Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view)</c> API this codebase's LOCAL
/// <see cref="WindowsRegistryReader"/> uses, there is no equivalent "give me the 32-bit view"
/// switch on a <c>StdRegProv</c> method call. A <see cref="RegistryView.Registry32"/> query
/// therefore returns <see cref="OperationStatus.Unsupported"/> immediately, before any network
/// call — never a silently-wrong (64-bit) answer presented as if it were the requested 32-bit
/// one. This narrows what <c>WindowsComScanner</c>'s <c>ComRegistrationSource.LocalMachine32</c>
/// probe can discover remotely (fewer 32-bit-only COM registrations found) — documented in
/// ARCHITECTURE.md's Phase 10D-3B addendum as a Known Limitation, not hacked around with a
/// guessed path rewrite.
/// </summary>
public sealed class WinRmRegistryOperations(CimWinRmTransport transport, TimeSpan operationTimeout) : IWindowsRemoteRegistryOperations
{
    private const uint HKeyClassesRoot = 0x80000000;
    private const uint HKeyCurrentUser = 0x80000001;
    private const uint HKeyLocalMachine = 0x80000002;
    private const uint HKeyUsers = 0x80000003;
    private const uint HKeyCurrentConfig = 0x80000005;

    public ScanTarget Target => transport.Target;

    public WindowsRemoteOperationResult<WindowsRegistryQueryResult> Query(WindowsRegistryQuery query)
    {
        if (query.View == RegistryView.Registry32)
        {
            return WindowsRemoteOperationResult<WindowsRegistryQueryResult>.Failure(
                OperationStatus.Unsupported, "StdRegProv has no 32-bit (WOW6432Node) view — remote Registry32 reads are not supported.");
        }

        var hive = MapHive(query.Hive);
        if (hive is null)
        {
            return WindowsRemoteOperationResult<WindowsRegistryQueryResult>.Failure(OperationStatus.Unsupported, $"Unsupported hive '{query.Hive}'.");
        }

        IReadOnlyList<string> subKeyNames = [];
        var values = new Dictionary<string, object?>();

        if (query.IncludeSubKeys)
        {
            var subKeysOutcome = InvokeStdRegProv("EnumKey", hive.Value, query.KeyPath, new Dictionary<string, object?>());
            if (subKeysOutcome.Status != OperationStatus.Success)
            {
                return WindowsRemoteOperationResult<WindowsRegistryQueryResult>.Failure(subKeysOutcome.Status, subKeysOutcome.ErrorMessage);
            }

            if (subKeysOutcome.ReturnValue == 0 && subKeysOutcome.OutParameters.GetValueOrDefault("sNames") is object?[] rawNames)
            {
                subKeyNames = rawNames.OfType<string>().ToList();
            }
        }

        if (query.IncludeValues)
        {
            var enumOutcome = InvokeStdRegProv("EnumValues", hive.Value, query.KeyPath, new Dictionary<string, object?>());
            if (enumOutcome.Status != OperationStatus.Success)
            {
                return WindowsRemoteOperationResult<WindowsRegistryQueryResult>.Failure(enumOutcome.Status, enumOutcome.ErrorMessage);
            }

            if (enumOutcome.ReturnValue == 0
                && enumOutcome.OutParameters.GetValueOrDefault("sNames") is object?[] rawValueNames
                && enumOutcome.OutParameters.GetValueOrDefault("Types") is object?[] rawTypes)
            {
                var names = rawValueNames.OfType<string>().ToArray();
                var types = rawTypes.Select(t => t is IConvertible c ? Convert.ToInt32(c) : -1).ToArray();
                var wanted = query.ValueNames.Count == 0 ? null : new HashSet<string>(query.ValueNames, StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < names.Length && i < types.Length; i++)
                {
                    var name = names[i];
                    if (wanted is not null && !wanted.Contains(name))
                    {
                        continue;
                    }

                    var value = ReadTypedValue(hive.Value, query.KeyPath, name, types[i]);
                    if (value is not NoValue)
                    {
                        values[name] = value is Skip ? null : value;
                    }
                }
            }
        }

        return WindowsRemoteOperationResult<WindowsRegistryQueryResult>.Ok(new WindowsRegistryQueryResult
        {
            SubKeyNames = subKeyNames,
            Values = values
        });
    }

    private sealed class NoValue;
    private sealed class Skip;

    private object? ReadTypedValue(uint hive, string keyPath, string valueName, int regType)
    {
        var method = regType switch
        {
            1 => "GetStringValue",          // REG_SZ
            2 => "GetExpandedStringValue",  // REG_EXPAND_SZ
            3 => "GetBinaryValue",          // REG_BINARY
            4 => "GetDWORDValue",           // REG_DWORD
            7 => "GetMultiStringValue",     // REG_MULTI_SZ
            _ => null
        };

        if (method is null)
        {
            return new Skip(); // an unrecognized type (e.g. REG_QWORD) is left out, not guessed at.
        }

        var outcome = InvokeStdRegProv(method, hive, keyPath, new Dictionary<string, object?> { ["sValueName"] = valueName });
        if (outcome.Status != OperationStatus.Success || outcome.ReturnValue != 0)
        {
            return new NoValue();
        }

        var outParamName = method switch
        {
            "GetStringValue" or "GetExpandedStringValue" => "sValue",
            "GetBinaryValue" => "uValue",
            "GetDWORDValue" => "uValue",
            "GetMultiStringValue" => "sValue",
            _ => null
        };

        var raw = outParamName is null ? null : outcome.OutParameters.GetValueOrDefault(outParamName);
        return regType switch
        {
            3 => raw as byte[] ?? (raw as object?[])?.Select(Convert.ToByte).ToArray(),
            7 => (raw as object?[])?.OfType<string>().ToArray() ?? Array.Empty<string>(),
            _ => raw
        };
    }

    private CimMethodOutcome InvokeStdRegProv(string method, uint hive, string keyPath, IReadOnlyDictionary<string, object?> extraParameters)
    {
        var parameters = new Dictionary<string, object?>(extraParameters)
        {
            ["hDefKey"] = hive,
            ["sSubKeyName"] = keyPath
        };

        return transport.InvokeAllowedMethod(
            WindowsWmiMethodAllowList.StdRegProvNamespace, WindowsWmiMethodAllowList.StdRegProvClass,
            null, method, parameters, operationTimeout, CancellationToken.None);
    }

    private static uint? MapHive(RegistryHive hive) => hive switch
    {
        RegistryHive.ClassesRoot => HKeyClassesRoot,
        RegistryHive.CurrentUser => HKeyCurrentUser,
        RegistryHive.LocalMachine => HKeyLocalMachine,
        RegistryHive.Users => HKeyUsers,
        RegistryHive.CurrentConfig => HKeyCurrentConfig,
        _ => null
    };
}
