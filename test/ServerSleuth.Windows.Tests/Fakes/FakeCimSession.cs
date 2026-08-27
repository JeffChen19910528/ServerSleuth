using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Tests.Fakes;

/// <summary>
/// A deterministic, in-memory-only <see cref="ICimSession"/> double — see skill.md (Phase
/// 10D-3B) §26. Never makes a network call of any kind; records every query/method invocation
/// so a test can assert on the exact structured request that was passed, and can be configured
/// to simulate every failure mode skill.md §26 lists (connection failure, authentication
/// failure/access denied, timeout, protocol error, cancellation).
/// </summary>
public sealed class FakeCimSession : ICimSession
{
    public bool ConnectCalled => ConnectCallCount > 0;
    public int ConnectCallCount { get; private set; }
    public int DisposeCallCount { get; private set; }
    public bool Disposed => DisposeCallCount > 0;
    public WinRmConnectException? ConnectFailure { get; set; }

    public List<(string Namespace, string Wql)> RecordedQueries { get; } = [];
    public List<(string Namespace, string ClassName, string MethodName)> RecordedMethodInvocations { get; } = [];

    public Func<string, string, CimQueryOutcome>? QueryHandler { get; set; }
    public Func<string, string, string, CimMethodOutcome>? MethodHandler { get; set; }

    public void Connect(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectCallCount++;
        if (ConnectFailure is not null)
        {
            throw ConnectFailure;
        }
    }

    public CimQueryOutcome QueryInstances(string ns, string wqlQuery, TimeSpan timeout, CancellationToken cancellationToken)
    {
        RecordedQueries.Add((ns, wqlQuery));
        cancellationToken.ThrowIfCancellationRequested();
        return QueryHandler?.Invoke(ns, wqlQuery) ?? CimQueryOutcome.Ok([]);
    }

    public CimMethodOutcome InvokeMethod(
        string ns, string className, IReadOnlyDictionary<string, object?>? instanceKeyProperties,
        string methodName, IReadOnlyDictionary<string, object?> parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        RecordedMethodInvocations.Add((ns, className, methodName));
        cancellationToken.ThrowIfCancellationRequested();

        if (!WindowsWmiMethodAllowList.IsAllowed(ns, className, methodName))
        {
            return CimMethodOutcome.Failure(OperationStatus.InvalidInput, "not allowed");
        }

        return MethodHandler?.Invoke(ns, className, methodName) ?? CimMethodOutcome.Ok(0, new Dictionary<string, object?>());
    }

    public void Dispose() => DisposeCallCount++;
}
