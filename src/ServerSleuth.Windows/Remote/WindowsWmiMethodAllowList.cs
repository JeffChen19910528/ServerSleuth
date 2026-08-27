namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The closed, hard-coded allow-list of (namespace, class, method) triples this codebase will
/// EVER invoke over WinRM — see skill.md (Phase 10D-3B) §10-11: "never query arbitrary WMI
/// classes supplied by user input... prefer an explicit allow-list." Every entry here is a
/// well-known, Microsoft-documented, side-effect-free READ operation:
///
/// - <c>root\default!StdRegProv</c>'s <c>EnumKey</c>/<c>EnumValues</c>/<c>GetStringValue</c>/
///   <c>GetExpandedStringValue</c>/<c>GetDWORDValue</c>/<c>GetBinaryValue</c>/
///   <c>GetMultiStringValue</c> — the registry READ subset only. <c>StdRegProv</c> also exposes
///   <c>SetStringValue</c>/<c>SetDWORDValue</c>/<c>CreateKey</c>/<c>DeleteKey</c>/etc.; none of
///   those appear here, and <see cref="CimWinRmTransport.InvokeMethod"/> rejects anything not
///   listed, so a write method could never be reached even by a coding mistake elsewhere in
///   this codebase — the read-only guarantee is structural, not just a convention every caller
///   has to remember (skill.md §9's "structurally read-only" requirement, enforced the same way
///   Phase 10D-2 enforced "no mutating systemctl verb").
/// - <c>root\cimv2!Win32_Process</c>'s <c>GetOwner</c> — the one instance method Phase 10D-3A's
///   disclosed gap named. Present in the allow-list (proving the capability CAN be represented
///   structurally, satisfying skill.md §11's "preferred solution"), but deliberately never
///   invoked from the bulk process-enumeration path (see
///   <see cref="ServerSleuth.Windows.Process.WinRmProcessWmiProvider"/>'s doc comment) — calling
///   it once per process would be an N+1 remote call per scan, which skill.md §29 explicitly
///   forbids trading against a "nice to have" field.
/// </summary>
public static class WindowsWmiMethodAllowList
{
    public const string StdRegProvNamespace = @"root\default";
    public const string StdRegProvClass = "StdRegProv";

    public static readonly IReadOnlyCollection<string> StdRegProvReadMethods =
    [
        "EnumKey", "EnumValues", "GetStringValue", "GetExpandedStringValue",
        "GetDWORDValue", "GetBinaryValue", "GetMultiStringValue"
    ];

    public const string Win32ProcessNamespace = @"root\cimv2";
    public const string Win32ProcessClass = "Win32_Process";
    public const string Win32ProcessGetOwnerMethod = "GetOwner";

    /// <summary>The closed allow-list of (namespace, class) pairs a <c>WindowsWmiQuery</c>
    /// (property-select query, never a method call) may target — skill.md §10's "investigate
    /// Win32_Process/MSFT_NetTCPConnection/MSFT_NetUDPEndpoint/service-related data" list, plus
    /// <c>Win32_Service</c> (the class Phase 10D-3A's folded-in <c>ServiceQuery</c> decision
    /// depends on).</summary>
    public static readonly IReadOnlyCollection<(string Namespace, string ClassName)> QueryableClasses =
    [
        (@"root\cimv2", "Win32_Process"),
        (@"root\cimv2", "Win32_Service"),
        (@"root\StandardCimv2", "MSFT_NetTCPConnection"),
        (@"root\StandardCimv2", "MSFT_NetUDPEndpoint")
    ];

    public static bool IsQueryable(string ns, string className) => QueryableClasses.Any(c =>
        string.Equals(c.Namespace, ns, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(c.ClassName, className, StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowed(string ns, string className, string methodName) => (ns, className, methodName) switch
    {
        _ when string.Equals(ns, StdRegProvNamespace, StringComparison.OrdinalIgnoreCase)
            && string.Equals(className, StdRegProvClass, StringComparison.OrdinalIgnoreCase)
            && StdRegProvReadMethods.Contains(methodName, StringComparer.Ordinal) => true,

        _ when string.Equals(ns, Win32ProcessNamespace, StringComparison.OrdinalIgnoreCase)
            && string.Equals(className, Win32ProcessClass, StringComparison.OrdinalIgnoreCase)
            && string.Equals(methodName, Win32ProcessGetOwnerMethod, StringComparison.Ordinal) => true,

        _ => false
    };
}
