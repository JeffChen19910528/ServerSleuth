using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Orchestration;

/// <summary>
/// Default <see cref="IDiscoveryEngine"/> — enumerates the registry's scanners in their fixed
/// deterministic order, executes each in turn, and never lets one scanner's failure (including
/// an unhandled exception, which a well-behaved scanner should never throw, but this engine
/// defends against regardless) abort the run for the rest. See skill.md (Phase 6G) §2, §7, §9.
/// Contains no `if (OperatingSystem.IsWindows())`/platform-specific branching whatsoever — which
/// scanners exist at all is entirely a function of what the composition root registered.
/// </summary>
public sealed class DiscoveryEngine(IDiscoveryScannerRegistry registry) : IDiscoveryEngine
{
    public async Task<AggregateDiscoveryResult> RunAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        var scannerResults = new List<DiscoveryResult>();
        var diagnostics = new List<string>();

        foreach (var scanner in registry.Scanners)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DiscoveryResult result;
            try
            {
                result = await scanner.ScanAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var message = $"{scanner.Id} threw an unhandled exception: {ex.GetType().Name}: {ex.Message}";
                diagnostics.Add(message);
                result = new DiscoveryResult
                {
                    ScannerId = scanner.Id,
                    Status = ScannerStatus.Failed,
                    Errors = [new DiscoveryError { ScannerId = scanner.Id, Message = message, Exception = ex.GetType().FullName }]
                };
            }

            scannerResults.Add(result);
        }

        return new AggregateDiscoveryResult
        {
            Entities = scannerResults.SelectMany(r => r.Entities).ToList(),
            Errors = scannerResults.SelectMany(r => r.Errors).ToList(),
            ScannerResults = scannerResults,
            ScannerStatuses = scannerResults.ToDictionary(r => r.ScannerId, r => r.Status, StringComparer.Ordinal),
            Diagnostics = diagnostics
        };
    }
}
