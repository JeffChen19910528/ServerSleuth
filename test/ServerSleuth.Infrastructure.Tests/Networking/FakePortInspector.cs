using ServerSleuth.Infrastructure.Networking;

namespace ServerSleuth.Infrastructure.Tests.Networking;

/// <summary>
/// Exercises the IPortInspector contract with canned data. No real Windows/Linux socket
/// enumeration exists yet — that arrives in Phase 3 / Phase 6 — so this stands in to prove
/// the interface and NetworkEndpoint DTO shape are usable by a caller today.
/// </summary>
internal sealed class FakePortInspector(IReadOnlyList<NetworkEndpoint> endpoints) : IPortInspector
{
    public Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(endpoints);
}
