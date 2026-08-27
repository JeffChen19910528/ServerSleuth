using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// One row from a CIM/WMI query — a flat property-name→value map, the same generic shape
/// <c>ManagementObjectSearcher</c> already produces locally (see
/// <see cref="ServerSleuth.Windows.Wmi.IWindowsRemoteWmiOperations"/>'s doc comment).
/// </summary>
public sealed record CimQueryOutcome
{
    public required OperationStatus Status { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public static CimQueryOutcome Ok(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => new() { Status = OperationStatus.Success, Rows = rows };
    public static CimQueryOutcome Failure(OperationStatus status, string? errorMessage = null) => new() { Status = status, ErrorMessage = errorMessage };
}

/// <summary>The outcome of one structured CIM method invocation (a <c>StdRegProv</c> registry
/// read or <c>Win32_Process.GetOwner</c> — see <see cref="WindowsWmiMethodAllowList"/>). Never
/// carries a raw method-invocation string — <see cref="ReturnValue"/>/<see cref="OutParameters"/>
/// are the SAME typed-value shape a <see cref="CimQueryOutcome"/> row uses.</summary>
public sealed record CimMethodOutcome
{
    public required OperationStatus Status { get; init; }
    public uint? ReturnValue { get; init; }
    public IReadOnlyDictionary<string, object?> OutParameters { get; init; } = new Dictionary<string, object?>();
    public string? ErrorMessage { get; init; }

    public static CimMethodOutcome Ok(uint returnValue, IReadOnlyDictionary<string, object?> outParameters) =>
        new() { Status = OperationStatus.Success, ReturnValue = returnValue, OutParameters = outParameters };

    public static CimMethodOutcome Failure(OperationStatus status, string? errorMessage = null) => new() { Status = status, ErrorMessage = errorMessage };
}

/// <summary>
/// The seam between this codebase and <c>Microsoft.Management.Infrastructure.CimSession</c> —
/// the Windows-domain counterpart to
/// <see cref="ServerSleuth.Infrastructure.Remote.ISshSession"/>. <see cref="CimNetSession"/> is
/// the only real implementation; a fake implementation stands in for every deterministic test
/// (skill.md Phase 10D-3B §26), so the entire security test suite runs with NO live WinRM host.
///
/// Exactly three operations — connect, query, invoke-one-named-method — mirroring how narrow
/// <c>ISshSession</c> already is. No member here takes a raw command/script string of any kind.
/// </summary>
public interface ICimSession : IDisposable
{
    void Connect(CancellationToken cancellationToken);

    CimQueryOutcome QueryInstances(string ns, string wqlQuery, TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>Invokes a named method — either a STATIC/class-level call (StdRegProv, when
    /// <paramref name="instanceKeyProperties"/> is <c>null</c>) or an INSTANCE call
    /// (<c>Win32_Process.GetOwner</c>, when <paramref name="instanceKeyProperties"/> identifies
    /// one instance by its key property/properties) — never an arbitrary method name; callers
    /// are expected to have already checked <see cref="WindowsWmiMethodAllowList"/>.</summary>
    CimMethodOutcome InvokeMethod(
        string ns,
        string className,
        IReadOnlyDictionary<string, object?>? instanceKeyProperties,
        string methodName,
        IReadOnlyDictionary<string, object?> parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
