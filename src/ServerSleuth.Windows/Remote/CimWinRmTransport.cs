using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The real "Windows Remote Transport" box in this phase's own architecture diagram — one
/// reusable <see cref="ICimSession"/> per <see cref="ScanTarget"/>, shared by every capability
/// implementation built on top of it (skill.md Phase 10D-3B §29: "one reusable transport
/// session per target," never one connection per query). Enforces
/// <see cref="WindowsWmiMethodAllowList"/> at the QUERY level too (not just method invocation)
/// — <see cref="Query"/> rejects any (namespace, class) pair not in
/// <see cref="WindowsWmiMethodAllowList.QueryableClasses"/> before the underlying
/// <see cref="ICimSession"/> is ever called, so a caller cannot accidentally reach an
/// unapproved WMI class even by constructing a <c>WindowsWmiQuery</c> for one.
///
/// Owns connection lifetime: <see cref="Connect"/> is always explicit, never implicit at
/// construction (skill.md §6/§8, matching Phase 10D-2's own SSH connection-timing rule);
/// <see cref="Dispose"/> releases the underlying session exactly once.
///
/// **Phase 10E-2 hardening**: <see cref="Query"/>/<see cref="InvokeAllowedMethod"/> both
/// defensively catch <see cref="OperationCanceledException"/> around the call into
/// <see cref="ICimSession"/> and convert it to <see cref="OperationStatus.Cancelled"/>, rather
/// than letting it propagate as a raw unhandled exception. The real <see cref="CimNetSession"/>
/// already does this internally (never throws for a cancelled operation), but nothing in
/// <see cref="ICimSession"/>'s own contract required an implementation to — this is
/// defense-in-depth so a cancelled query is NEVER converted into an unhandled exception by this
/// transport regardless of which <see cref="ICimSession"/> implementation is behind it,
/// matching skill.md (Phase 10E-2) §10's "cancellation gets converted into fake success" ==
/// FORBIDDEN, but an unhandled exception escaping the intended boundary is equally forbidden.
/// </summary>
public sealed class CimWinRmTransport(ScanTarget target, ICimSession session) : IDisposable
{
    public ScanTarget Target { get; } = target;

    public void Connect(CancellationToken cancellationToken) => session.Connect(cancellationToken);

    public WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Query(
        string ns, string className, IReadOnlyList<string> properties, string? whereClause, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!WindowsWmiMethodAllowList.IsQueryable(ns, className))
        {
            return WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>.Failure(
                OperationStatus.InvalidInput, $"'{ns}!{className}' is not on the query allow-list.");
        }

        var wql = whereClause is null
            ? $"SELECT {string.Join(", ", properties)} FROM {className}"
            : $"SELECT {string.Join(", ", properties)} FROM {className} WHERE {whereClause}";

        try
        {
            var outcome = session.QueryInstances(ns, wql, timeout, cancellationToken);

            return outcome.Status == OperationStatus.Success
                ? WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>.Ok(outcome.Rows)
                : WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>.Failure(outcome.Status, outcome.ErrorMessage);
        }
        catch (OperationCanceledException)
        {
            return WindowsRemoteOperationResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>.Failure(OperationStatus.Cancelled);
        }
    }

    public CimMethodOutcome InvokeAllowedMethod(
        string ns, string className, IReadOnlyDictionary<string, object?>? instanceKeyProperties,
        string methodName, IReadOnlyDictionary<string, object?> parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return session.InvokeMethod(ns, className, instanceKeyProperties, methodName, parameters, timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return CimMethodOutcome.Failure(OperationStatus.Cancelled);
        }
    }

    public void Dispose() => session.Dispose();
}
