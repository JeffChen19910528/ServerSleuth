namespace ServerSleuth.Windows.Wmi;

/// <summary>
/// A structured WMI class query — see skill.md (Phase 10D-3A) §7. Represents exactly the shape
/// every existing Windows WMI caller already builds by hand as a WQL string:
/// <see cref="ServerSleuth.Windows.Process.ProcessWmiProvider"/>'s
/// <c>"SELECT ProcessId, ExecutablePath, CommandLine, ParentProcessId FROM Win32_Process"</c>
/// and <see cref="ServerSleuth.Windows.Networking.NetworkTableProvider"/>'s
/// <c>"SELECT LocalAddress, LocalPort, OwningProcess FROM MSFT_NetTCPConnection WHERE State = 2"</c>
/// (root\StandardCimv2). <see cref="Namespace"/>/<see cref="ClassName"/>/<see cref="Properties"/>
/// (the <c>SELECT</c> list) and <see cref="Filters"/> (the <c>WHERE</c> clauses, ANDed together
/// — sufficient for both existing callers, neither of which needs OR/nesting) are held as
/// separate typed fields, never assembled into a WQL string here — building that string safely
/// is a future transport's job, exactly as <c>SshCommandLineBuilder</c> is the one place a
/// structured <c>ProcessQuery</c> becomes an SSH exec-channel string.
///
/// Deliberately has NO method-name field, NO method-arguments field, and NO raw-WQL-string
/// field: <see cref="ServerSleuth.Windows.Process.ProcessWmiProvider.TryGetOwner"/>'s
/// <c>InvokeMethod("GetOwner", null)</c> call — used to populate
/// <see cref="ServerSleuth.Windows.Process.ProcessWmiInfo.OwnerDomain"/>/<c>OwnerUser</c> — is
/// therefore a DISCLOSED interface gap this capability model does not cover (skill.md §21: "if
/// an existing scanner cannot be represented ... STOP and document the exact gap. Do not hack
/// around it."). A remote <see cref="WindowsWmiQuery"/> can never populate those two fields;
/// see ARCHITECTURE.md's Phase 10D-3A addendum for the full reasoning, including why this
/// phase does not invent a narrow "well-known read-only method" allow-list the way Phase
/// 10D-2's <c>readlink</c> exception did for SSH (no read-only-vs-mutating classification for
/// arbitrary WMI methods exists anywhere in this codebase to build such an allow-list from).
///
/// Also represents a future <c>Win32_Service</c> query (<c>Name</c>/<c>DisplayName</c>/
/// <c>State</c>/<c>StartMode</c> properties, root\cimv2) — the reason
/// <c>WindowsRemoteOperationKind</c> has no separate <c>ServiceQuery</c> member. See that
/// enum's own doc comment.
/// </summary>
public sealed record WindowsWmiQuery
{
    public required string Namespace { get; init; }
    public required string ClassName { get; init; }
    public required IReadOnlyList<string> Properties { get; init; }
    public IReadOnlyList<WmiFilterClause> Filters { get; init; } = [];

    public static readonly string StandardCimv2Namespace = @"root\StandardCimv2";
    public static readonly string Cimv2Namespace = @"root\cimv2";
}
