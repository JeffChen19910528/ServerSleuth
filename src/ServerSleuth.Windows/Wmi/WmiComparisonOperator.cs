namespace ServerSleuth.Windows.Wmi;

/// <summary>
/// The closed set of comparisons a <see cref="WmiFilterClause"/> may express — see skill.md
/// (Phase 10D-3A) §7. Sized to exactly what the existing Windows WMI queries need:
/// <see cref="ServerSleuth.Windows.Networking.NetworkTableProvider"/>'s
/// <c>WHERE State = 2</c> clause is the only filter any current Windows scanner uses. Not a
/// general-purpose WQL expression grammar (no <c>LIKE</c>, no <c>AND</c>/<c>OR</c> composition,
/// no numeric comparison operators) — extending this enum is explicit future work, to be done
/// only when a genuine new filtering need is found, never speculatively.
/// </summary>
public enum WmiComparisonOperator
{
    Equals,
    NotEquals
}
