using ServerSleuth.Core.Targets;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Wmi;

/// <summary>
/// The real <see cref="IWindowsRemoteWmiOperations"/> implementation — turns a structured
/// <see cref="WindowsWmiQuery"/> into one <c>SELECT ... FROM ... WHERE ...</c> WQL string
/// (built here, the one place, exactly like <c>SshCommandLineBuilder</c> builds an SSH exec
/// string) and hands it to the shared <see cref="CimWinRmTransport"/>, which independently
/// re-checks the (namespace, class) pair against <see cref="WindowsWmiMethodAllowList"/> before
/// any network call — so even a caller that bypassed this class could not reach an unapproved
/// class through the transport.
///
/// Only the closed <see cref="WmiComparisonOperator.Equals"/>/<see cref="WmiComparisonOperator.NotEquals"/>
/// filter shapes exist (Phase 10D-3A's own design) — property VALUES are still passed as
/// separate WQL string literals (quoted here, never concatenated raw), so a filter value
/// containing a quote character cannot escape the literal it was placed in.
/// </summary>
public sealed class WinRmWmiOperations(CimWinRmTransport transport, TimeSpan operationTimeout) : IWindowsRemoteWmiOperations
{
    public ScanTarget Target => transport.Target;

    public WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Query(WindowsWmiQuery query)
    {
        var whereClause = query.Filters.Count == 0 ? null : string.Join(" AND ", query.Filters.Select(BuildClause));

        return transport.Query(query.Namespace, query.ClassName, query.Properties, whereClause, operationTimeout, CancellationToken.None);
    }

    private static string BuildClause(WmiFilterClause clause)
    {
        var op = clause.Operator == WmiComparisonOperator.Equals ? "=" : "!=";
        var quotedValue = IsNumeric(clause.Value) ? clause.Value : $"'{clause.Value.Replace("'", "''", StringComparison.Ordinal)}'";
        return $"{clause.PropertyName} {op} {quotedValue}";
    }

    private static bool IsNumeric(string value) => long.TryParse(value, out _);
}
