using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Tests.Orchestration;

public class DiscoveryEngineTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task RunAsync_MultipleScanners_AggregatesEntitiesErrorsAndStatuses()
    {
        var scannerA = new ConfigurableFakeScanner("scanner-a", entities: [ConfigurableFakeScanner.MakeEntity("a-1")]);
        var scannerB = new ConfigurableFakeScanner("scanner-b", entities: [ConfigurableFakeScanner.MakeEntity("b-1"), ConfigurableFakeScanner.MakeEntity("b-2")]);
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([scannerA, scannerB]));

        var result = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Equal(3, result.Entities.Count);
        Assert.Equal(2, result.ScannerResults.Count);
        Assert.Equal(ScannerStatus.Supported, result.ScannerStatuses["scanner-a"]);
        Assert.Equal(ScannerStatus.Supported, result.ScannerStatuses["scanner-b"]);
    }

    [Fact]
    public async Task RunAsync_OnePartiallySupportedScanner_DoesNotAffectOtherScanners()
    {
        var partial = new ConfigurableFakeScanner("partial-scanner", status: ScannerStatus.PartiallySupported,
            errors: [new DiscoveryError { ScannerId = "partial-scanner", Message = "one file unreadable" }]);
        var supported = new ConfigurableFakeScanner("supported-scanner", entities: [ConfigurableFakeScanner.MakeEntity("s-1")]);
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([partial, supported]));

        var result = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.ScannerStatuses["partial-scanner"]);
        Assert.Equal(ScannerStatus.Supported, result.ScannerStatuses["supported-scanner"]);
        Assert.Single(result.Entities); // only from supported-scanner
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task RunAsync_AccessDeniedScanner_RemainsFullyVisible_NeverDropped()
    {
        var denied = new ConfigurableFakeScanner("denied-scanner", status: ScannerStatus.AccessDenied,
            errors: [new DiscoveryError { ScannerId = "denied-scanner", Message = "denied", IsPermissionFailure = true }]);
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([denied]));

        var result = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Single(result.ScannerResults);
        Assert.Equal(ScannerStatus.AccessDenied, result.ScannerStatuses["denied-scanner"]);
        Assert.True(result.Errors.Single().IsPermissionFailure);
    }

    [Fact]
    public async Task RunAsync_OneScannerThrows_DegradesToFailed_OtherScannersStillRun_NeverAbortsWholeRun()
    {
        var throwing = new ConfigurableFakeScanner("throwing-scanner", throwOnScan: new InvalidOperationException("boom"));
        var healthy = new ConfigurableFakeScanner("healthy-scanner", entities: [ConfigurableFakeScanner.MakeEntity("h-1")]);
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([throwing, healthy]));

        var result = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.ScannerStatuses["throwing-scanner"]);
        Assert.Equal(ScannerStatus.Supported, result.ScannerStatuses["healthy-scanner"]);
        Assert.Single(result.Entities); // only from healthy-scanner
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Contains("throwing-scanner"));
    }

    [Fact]
    public async Task RunAsync_DuplicateLogicalObservationsFromTwoScanners_NeverMerged_BothPreserved()
    {
        // Two different scanners producing entities with different IDs for what a human might
        // consider "the same logical thing" — the engine must never perform unsafe heuristic
        // merging; that is Analysis's job, not the orchestrator's. See skill.md (Phase 6G) §8.
        var scannerA = new ConfigurableFakeScanner("scanner-a", entities: [ConfigurableFakeScanner.MakeEntity("process:100", "erp")]);
        var scannerB = new ConfigurableFakeScanner("scanner-b", entities: [ConfigurableFakeScanner.MakeEntity("service:erp.service", "erp")]);
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([scannerA, scannerB]));

        var result = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Equal(2, result.Entities.Count);
        Assert.Contains(result.Entities, e => e.Id == "process:100");
        Assert.Contains(result.Entities, e => e.Id == "service:erp.service");
    }

    [Fact]
    public async Task RunAsync_CalledTwiceWithSameScanners_ProducesIdenticalOrderingAndIds_Deterministic()
    {
        var scannerA = new ConfigurableFakeScanner("scanner-a", entities: [ConfigurableFakeScanner.MakeEntity("a-1")]);
        var scannerB = new ConfigurableFakeScanner("scanner-b", entities: [ConfigurableFakeScanner.MakeEntity("b-1")]);
        var scannerC = new ConfigurableFakeScanner("scanner-c", entities: [ConfigurableFakeScanner.MakeEntity("c-1")]);
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([scannerC, scannerA, scannerB])); // deliberately out-of-order construction

        var resultA = await engine.RunAsync(Context(), CancellationToken.None);
        var resultB = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Equal(resultA.Entities.Select(e => e.Id), resultB.Entities.Select(e => e.Id));
        Assert.Equal(resultA.ScannerResults.Select(r => r.ScannerId), resultB.ScannerResults.Select(r => r.ScannerId));
        Assert.Equal(["scanner-a", "scanner-b", "scanner-c"], resultA.ScannerResults.Select(r => r.ScannerId));
    }

    [Fact]
    public async Task RunAsync_EmptyRegistry_ReturnsEmptyAggregateResult_NeverThrows()
    {
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([]));

        var result = await engine.RunAsync(Context(), CancellationToken.None);

        Assert.Empty(result.Entities);
        Assert.Empty(result.Errors);
        Assert.Empty(result.ScannerResults);
    }

    [Fact]
    public async Task RunAsync_CancellationRequested_ThrowsOperationCanceled_NeverSwallowed()
    {
        var scanner = new ConfigurableFakeScanner("scanner-a");
        var engine = new DiscoveryEngine(new DiscoveryScannerRegistry([scanner]));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.RunAsync(Context(), cts.Token));
    }
}
