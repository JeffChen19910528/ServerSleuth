using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Cli.Tests.Fakes;

/// <summary>Builds a synthetic <see cref="AggregateDiscoveryResult"/> from a fixed entity list
/// plus optional extra <see cref="DiscoveryResult"/> entries (e.g. a scanner reporting
/// AccessDenied) — mirrors what the real <c>DiscoveryEngine</c> would have produced.</summary>
internal static class DiscoveryResultBuilder
{
    public static AggregateDiscoveryResult Build(IReadOnlyList<DiscoveryEntity> entities, params DiscoveryResult[] extraScannerResults)
    {
        var scannerResults = new List<DiscoveryResult>
        {
            new() { ScannerId = "fake-scanner", Status = ScannerStatus.Supported, Entities = entities }
        };
        scannerResults.AddRange(extraScannerResults);

        return new AggregateDiscoveryResult
        {
            Entities = entities,
            Errors = scannerResults.SelectMany(r => r.Errors).ToList(),
            ScannerResults = scannerResults,
            ScannerStatuses = scannerResults.ToDictionary(r => r.ScannerId, r => r.Status, StringComparer.Ordinal)
        };
    }
}
