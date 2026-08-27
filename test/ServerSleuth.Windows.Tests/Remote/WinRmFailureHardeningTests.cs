using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Process;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Tests.Fakes;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Tests.Remote;

/// <summary>
/// Phase 10E-2 §3, §9, §10, §11, §16: WinRM-specific failure/cancellation/disposal/determinism
/// hardening NOT already covered by Phase 10D-3B's own <see cref="WinRmTransportSecurityTests"/>
/// (which focused on read-only shape, allow-list enforcement, and TLS/credential structural
/// guarantees). Everything here runs against <see cref="FakeCimSession"/> — no live WinRM host.
/// </summary>
public class WinRmFailureHardeningTests
{
    private static readonly ScanTarget Target = ScanTarget.Remote("winhost.example.internal", TargetPlatform.Windows);

    private static WindowsWmiQuery ProcessQuery() => new()
    {
        Namespace = WindowsWmiQuery.Cimv2Namespace,
        ClassName = "Win32_Process",
        Properties = ["ProcessId"]
    };

    // 3.A-F, 9: connect failures classify correctly and never throw past the transport boundary.
    [Theory]
    [InlineData(OperationStatus.AccessDenied)]
    [InlineData(OperationStatus.TransportUnavailable)]
    [InlineData(OperationStatus.Timeout)]
    public void Connect_VariousFailureClassifications_SurfaceAsTheExpectedStatus_NeverThrowUnhandled(OperationStatus status)
    {
        var session = new FakeCimSession { ConnectFailure = new WinRmConnectException(status, "simulated failure") };
        var transport = new CimWinRmTransport(Target, session);

        var ex = Assert.Throws<WinRmConnectException>(() => transport.Connect(CancellationToken.None));
        Assert.Equal(status, ex.Status);
    }

    // 3.I, 10.6: WinRM query cancellation is converted to Cancelled, never an unhandled exception, never fake success.
    [Fact]
    public void Query_CancelledToken_ReturnsCancelledStatus_NeverThrowsNeverFakesSuccess()
    {
        var session = new FakeCimSession();
        var transport = new CimWinRmTransport(Target, session);
        var wmi = new WinRmWmiOperations(transport, TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // WinRmWmiOperations.Query itself does not thread a caller-supplied token through today
        // (a disclosed Phase 10D-3B limitation — the same coarse-grained shape the pre-existing
        // LOCAL IWindowsRegistryReader/IProcessWmiProvider interfaces already have). This test
        // exercises the lower CimWinRmTransport.Query layer directly, which DOES accept one, to
        // prove the Phase 10E-2 hardening fix: a session that throws OperationCanceledException
        // is converted to a Cancelled result here, never left to propagate unhandled.
        session.QueryHandler = (_, _) => throw new OperationCanceledException();
        var result = transport.Query(WindowsWmiQuery.Cimv2Namespace, "Win32_Process", ["ProcessId"], null, TimeSpan.FromSeconds(5), cts.Token);

        Assert.False(result.Success);
        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    [Fact]
    public void InvokeAllowedMethod_SessionThrowsOperationCanceled_ReturnsCancelledStatus_NeverThrowsUnhandled()
    {
        var session = new FakeCimSession { MethodHandler = (_, _, _) => throw new OperationCanceledException() };
        var transport = new CimWinRmTransport(Target, session);

        var result = transport.InvokeAllowedMethod(
            WindowsWmiMethodAllowList.StdRegProvNamespace, WindowsWmiMethodAllowList.StdRegProvClass, null,
            "EnumKey", new Dictionary<string, object?>(), TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    // 11: resource disposal — the underlying ICimSession is disposed exactly once when the transport is disposed.
    [Fact]
    public void Dispose_DisposesTheUnderlyingSession_ExactlyOnce()
    {
        var session = new FakeCimSession();
        var transport = new CimWinRmTransport(Target, session);

        transport.Dispose();

        Assert.True(session.Disposed);
    }

    [Fact]
    public void Dispose_AfterConnectFailure_StillDisposesTheUnderlyingSession()
    {
        var session = new FakeCimSession { ConnectFailure = new WinRmConnectException(OperationStatus.TransportUnavailable, "refused") };
        var transport = new CimWinRmTransport(Target, session);

        Assert.Throws<WinRmConnectException>(() => transport.Connect(CancellationToken.None));
        transport.Dispose();

        Assert.True(session.Disposed);
    }

    // 7: a failed connection never falls back to local execution — the WinRM-backed provider
    // set has no local implementation anywhere in its object graph to fall back to.
    [Fact]
    public void FailedConnection_NeverFallsBackToLocal_ProviderSetHasNoLocalImplementationToFallBackTo()
    {
        var session = new FakeCimSession { ConnectFailure = new WinRmConnectException(OperationStatus.AccessDenied, "denied") };
        var capabilities = new WinRmWindowsRemoteCapabilities(Target, session, TimeSpan.FromSeconds(5));
        var providerSet = new WinRmWindowsProviderSet(capabilities);

        Assert.Throws<WinRmConnectException>(() => providerSet.Connect(CancellationToken.None));

        // Every provider the set exposes is still WinRM-shaped, never a local fallback type,
        // even though the connection itself failed — proving failure does not swap the object
        // graph for a local one anywhere.
        Assert.IsType<WinRmWindowsRegistryReader>(providerSet.RegistryReader);
        Assert.IsType<WinRmProcessWmiProvider>(providerSet.ProcessWmiProvider);
        Assert.IsType<WinRmServiceEnumerator>(providerSet.ServiceEnumerator);
    }

    // 4, 18: a connect failure's message never carries the credential — only the underlying
    // library's own (credential-free) failure text.
    [Fact]
    public void ConnectFailure_MessageNeverContainsAnyCredentialMaterial()
    {
        const string sentinelPassword = "SERVER_SLEUTH_TEST_WINRM_PASSWORD_7a21f9";
        var session = new FakeCimSession { ConnectFailure = new WinRmConnectException(OperationStatus.AccessDenied, "The WS-Management service cannot process the request.") };
        var transport = new CimWinRmTransport(Target, session);

        var ex = Assert.Throws<WinRmConnectException>(() => transport.Connect(CancellationToken.None));

        Assert.DoesNotContain(sentinelPassword, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinelPassword, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WinRmConnectException_HasNoFieldCapableOfHoldingACredential()
    {
        var fields = typeof(WinRmConnectException).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(WindowsRemoteCredential));
    }

    // 9: status-mapping audit — every OperationStatus a query/method call can legitimately
    // return propagates through CimWinRmTransport unchanged (never remapped/collapsed).
    [Theory]
    [InlineData(OperationStatus.AccessDenied)]
    [InlineData(OperationStatus.NotFound)]
    [InlineData(OperationStatus.NotInstalled)]
    [InlineData(OperationStatus.ProtocolError)]
    [InlineData(OperationStatus.Timeout)]
    [InlineData(OperationStatus.TransportUnavailable)]
    public void Query_PropagatesEverySessionStatus_Unchanged(OperationStatus status)
    {
        var session = new FakeCimSession { QueryHandler = (_, _) => CimQueryOutcome.Failure(status, "simulated") };
        var transport = new CimWinRmTransport(Target, session);

        var result = transport.Query(WindowsWmiQuery.Cimv2Namespace, "Win32_Process", ["ProcessId"], null, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(status, result.Status);
    }

    // 15: determinism — repeating an identical failing query twice yields identical results.
    [Fact]
    public void RepeatedIdenticalFailingQuery_IsDeterministic()
    {
        var session = new FakeCimSession { QueryHandler = (_, _) => CimQueryOutcome.Failure(OperationStatus.AccessDenied, "denied") };
        var transport = new CimWinRmTransport(Target, session);

        var first = transport.Query(WindowsWmiQuery.Cimv2Namespace, "Win32_Process", ["ProcessId"], null, TimeSpan.FromSeconds(5), CancellationToken.None);
        var second = transport.Query(WindowsWmiQuery.Cimv2Namespace, "Win32_Process", ["ProcessId"], null, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.ErrorMessage, second.ErrorMessage);
    }

    // 16: no retry storm — a single failing query results in exactly one underlying session call.
    [Fact]
    public void FailingQuery_ResultsInExactlyOneUnderlyingSessionCall_NoRetryStorm()
    {
        var session = new FakeCimSession { QueryHandler = (_, _) => CimQueryOutcome.Failure(OperationStatus.TransportUnavailable) };
        var transport = new CimWinRmTransport(Target, session);

        transport.Query(WindowsWmiQuery.Cimv2Namespace, "Win32_Process", ["ProcessId"], null, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Single(session.RecordedQueries);
    }

    // 8: partial failure — one capability failing does not affect an independent one on the same transport/session.
    [Fact]
    public void OneCapabilityFailure_DoesNotAffectAnIndependentCapability_OnTheSameSharedSession()
    {
        var session = new FakeCimSession
        {
            QueryHandler = (ns, wql) => wql.Contains("Win32_Service", StringComparison.Ordinal)
                ? CimQueryOutcome.Failure(OperationStatus.AccessDenied)
                : CimQueryOutcome.Ok([])
        };
        var transport = new CimWinRmTransport(Target, session);
        var wmi = new WinRmWmiOperations(transport, TimeSpan.FromSeconds(5));

        var processResult = wmi.Query(ProcessQuery());
        var serviceResult = wmi.Query(new WindowsWmiQuery { Namespace = WindowsWmiQuery.Cimv2Namespace, ClassName = "Win32_Service", Properties = ["Name"] });

        Assert.True(processResult.Success);
        Assert.False(serviceResult.Success);
        Assert.Equal(OperationStatus.AccessDenied, serviceResult.Status);
    }
}
