namespace ServerSleuth.Infrastructure.Networking;

/// <summary>
/// Cross-platform contract for enumerating listening/active network endpoints. Real Windows
/// (Phase 3) and Linux (Phase 6) implementations arrive later — this interface plus the
/// NetworkEndpoint DTO exist now so scanners can be designed against it. See IMPLEMENTATION_PLAN.md.
/// </summary>
public interface IPortInspector
{
    Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken);
}
