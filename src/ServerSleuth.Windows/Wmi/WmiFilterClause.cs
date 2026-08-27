namespace ServerSleuth.Windows.Wmi;

/// <summary>
/// One typed <c>WHERE</c> clause term for a <see cref="WindowsWmiQuery"/> — see skill.md (Phase
/// 10D-3A) §7's instruction to "constrain [a filter] to a typed/validated representation where
/// practical." <see cref="Value"/> is a plain data string handed to whichever future transport
/// builds the actual WQL text (analogous to how
/// <see cref="ServerSleuth.Infrastructure.Remote.SshCommandLineBuilder"/> is the one place,
/// inside the transport only, where structured data becomes a wire-format string) — never
/// pre-formatted WQL syntax itself. <see cref="PropertyName"/> is validated the same way
/// (a bare identifier, never `"="`/quotes/parentheses of its own), so no combination of the
/// three fields can smuggle additional WQL syntax past a future transport's own quoting.
/// </summary>
public sealed record WmiFilterClause
{
    public required string PropertyName { get; init; }
    public required WmiComparisonOperator Operator { get; init; }
    public required string Value { get; init; }
}
