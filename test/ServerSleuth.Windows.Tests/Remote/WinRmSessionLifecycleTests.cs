using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.Tests.Fakes;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Tests.Remote;

/// <summary>
/// Phase 10E-3 §A-B: session-reuse and lifecycle guarantees NOT already covered by Phase
/// 10D-3B's/10E-2's own suites (which proved connect-once/dispose-once and no-retry-storm for a
/// SINGLE call). This suite proves the SAME <see cref="ICimSession"/> is reused across MULTIPLE,
/// DIFFERENT capabilities (Registry + WMI + Service, the actual mix a real Windows scan makes),
/// that a partial capability failure never disposes the shared session out from under a later,
/// independent capability, and that disposing the provider set twice is harmless.
/// </summary>
public class WinRmSessionLifecycleTests
{
    private static readonly ScanTarget Target = ScanTarget.Remote("winhost.example.internal", TargetPlatform.Windows);

    [Fact]
    public void MultipleDifferentCapabilities_AllReuseTheSameSession_NeverReconnect()
    {
        var session = new FakeCimSession();
        var capabilities = new WinRmWindowsRemoteCapabilities(Target, session, TimeSpan.FromSeconds(5));
        using var providerSet = new WinRmWindowsProviderSet(capabilities);

        providerSet.Connect(CancellationToken.None);
        Assert.True(session.ConnectCalled);

        // Registry (StdRegProv method invocation), WMI (Win32_Process query), and Service
        // (Win32_Service query, also WMI) — the same mix a real Windows scan issues — all
        // through the SAME provider set, backed by the SAME session.
        providerSet.RegistryReader.GetSubKeyNames(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64, @"SOFTWARE\Test");
        providerSet.ProcessWmiProvider.GetAll();
        providerSet.ServiceEnumerator.GetSnapshots();

        // No hidden per-capability reconnect — Connect was only ever called once, above.
        Assert.Equal(1, session.ConnectCallCount);
    }

    [Fact]
    public void MultipleAccessesToTheSameProperty_ReturnTheIdenticalProviderInstance()
    {
        var session = new FakeCimSession();
        var capabilities = new WinRmWindowsRemoteCapabilities(Target, session, TimeSpan.FromSeconds(5));
        using var providerSet = new WinRmWindowsProviderSet(capabilities);

        Assert.Same(providerSet.RegistryReader, providerSet.RegistryReader);
        Assert.Same(providerSet.ProcessWmiProvider, providerSet.ProcessWmiProvider);
    }

    // B: a partial (single-capability) failure never disposes or invalidates the shared session
    // — a later, independent capability call on the same provider set still succeeds normally.
    [Fact]
    public void OneFailedCapabilityCall_NeverDisposesTheSharedSession_LaterCallsStillWork()
    {
        var callCount = 0;
        var session = new FakeCimSession
        {
            QueryHandler = (_, wql) =>
            {
                callCount++;
                return wql.Contains("Win32_Service", StringComparison.Ordinal)
                    ? throw new InvalidOperationException("simulated transient WMI failure")
                    : CimQueryOutcome.Ok([]);
            }
        };
        var capabilities = new WinRmWindowsRemoteCapabilities(Target, session, TimeSpan.FromSeconds(5));
        using var providerSet = new WinRmWindowsProviderSet(capabilities);
        providerSet.Connect(CancellationToken.None);

        // Win32_Service query throws inside the fake — CimWinRmTransport.Query only special-cases
        // OperationCanceledException, so a generic exception propagates here, exactly like a real
        // ICimSession failure not classified as cancellation would. The point of this test is what
        // happens to the SESSION afterward, not this call's own result.
        Assert.ThrowsAny<Exception>(() => providerSet.ServiceEnumerator.GetSnapshots());

        Assert.False(session.Disposed);

        // A subsequent, independent, successful WMI call on the SAME provider set still works.
        var processes = providerSet.ProcessWmiProvider.GetAll();
        Assert.NotNull(processes);
        Assert.True(callCount >= 2);
    }

    // B: repeated disposal is harmless and deterministic.
    [Fact]
    public void DisposingTwice_IsHarmless_UnderlyingSessionDisposedTwice_NoException()
    {
        var session = new FakeCimSession();
        var capabilities = new WinRmWindowsRemoteCapabilities(Target, session, TimeSpan.FromSeconds(5));
        var providerSet = new WinRmWindowsProviderSet(capabilities);
        providerSet.Connect(CancellationToken.None);

        providerSet.Dispose();
        var exception = Record.Exception(providerSet.Dispose);

        Assert.Null(exception);
        Assert.Equal(2, session.DisposeCallCount); // both calls reached the underlying session — deterministic, not silently swallowed either.
    }
}
