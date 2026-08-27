using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Infrastructure.DependencyInjection;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Cli.Tests.Fakes;

/// <summary>Builds a minimal <see cref="IServiceProvider"/> containing a fake
/// <see cref="IDiscoveryEngine"/> — CLI unit tests never compose the real Windows/Linux scanner
/// registrations (skill.md Phase 10A §22) — plus the real, unmodified
/// <c>AddServerSleuthInfrastructure()</c> registrations (Phase 10C §3: <c>ITargetTransport</c>
/// among them), since <c>ScanCommand</c> now resolves the local target transport the exact same
/// way the real CLI composition root does.
///
/// Phase 10E-1 §14: <paramref name="transport"/> lets a REMOTE-PIPELINE-INTEGRATION test supply
/// a <see cref="ITargetTransport"/> carrying a <see cref="ServerSleuth.Core.Targets.ScanTarget.Kind"/>
/// of <c>Remote</c> — proving the SAME <c>ScanCommand</c>/<c>ScanPipelineRunner</c> code path
/// processes remote-origin discovery data identically to local, without connecting to any real
/// remote host (real connect semantics are already exhaustively covered by Phase 10D-2/10D-3B's
/// own `ISshSession`/`ICimSession`-fake-backed suites — not duplicated here). A plain,
/// non-`SshRemoteTargetTransport`/non-`WindowsRemoteTargetTransport` double is deliberate:
/// <c>ScanCommand</c>'s <c>Connect()</c> branch only triggers for those two concrete types, so
/// this double skips straight to discovery, exactly matching what a fake
/// <see cref="IDiscoveryEngine"/> already does for the discovery stage itself.</summary>
internal static class TestServiceProviderFactory
{
    public static IServiceProvider Build(IDiscoveryEngine discoveryEngine, ITargetTransport? transport = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServerSleuthInfrastructure(transport);
        services.AddSingleton(discoveryEngine);
        return services.BuildServiceProvider();
    }
}
