namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>
/// One detector per runtime family — never a single scanner with hard-coded if/else branches
/// per runtime (skill.md's explicit architecture requirement for Phase 4D). A detector never
/// touches DiscoveryResult directly; RuntimeDiscoveryScanner aggregates every detector's rows
/// independently, so one detector's failure never affects another's.
/// </summary>
public interface IRuntimeDetector
{
    string Id { get; }
    string RuntimeFamily { get; }

    Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken);
}
