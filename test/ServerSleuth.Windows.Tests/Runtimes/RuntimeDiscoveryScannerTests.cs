using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Windows.Runtimes;

namespace ServerSleuth.Windows.Tests.Runtimes;

internal sealed class FakeRuntimeDetector(string id, string family, RuntimeDetectionResult result) : IRuntimeDetector
{
    public string Id => id;
    public string RuntimeFamily => family;
    public Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken) => Task.FromResult(result);
}

internal sealed class ThrowingRuntimeDetector : IRuntimeDetector
{
    public string Id => "throwing-detector";
    public string RuntimeFamily => "Broken";
    public Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
}

public class RuntimeDiscoveryScannerTests
{
    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Deep, CancellationToken = CancellationToken.None };

    private static RuntimeDetectionRow MakeRow(string family) => new()
    {
        Family = family,
        EntityKind = RuntimeEntityKind.Runtime,
        Name = family,
        Version = "1.0",
        DetectionSources = ["Command"]
    };

    [Fact]
    public async Task ScanAsync_AllDetectorsFindSomething_ReturnsSupported()
    {
        var detectors = new IRuntimeDetector[]
        {
            new FakeRuntimeDetector("d1", "DotNetRuntime", RuntimeDetectionResult.Detected([MakeRow("DotNetRuntime")])),
            new FakeRuntimeDetector("d2", "Java", RuntimeDetectionResult.Detected([MakeRow("Java")]))
        };

        var scanner = new RuntimeDiscoveryScanner(detectors, NullLogger<RuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OneDetectorNotDetected_DoesNotDegradeOverallStatus()
    {
        var detectors = new IRuntimeDetector[]
        {
            new FakeRuntimeDetector("d1", "DotNetRuntime", RuntimeDetectionResult.Detected([MakeRow("DotNetRuntime")])),
            new FakeRuntimeDetector("d2", "Go", RuntimeDetectionResult.NotDetected())
        };

        var scanner = new RuntimeDiscoveryScanner(detectors, NullLogger<RuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status); // "not detected" is not an error
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_OneDetectorPartial_ReturnsOverallPartiallySupportedWithError()
    {
        var detectors = new IRuntimeDetector[]
        {
            new FakeRuntimeDetector("d1", "Php", RuntimeDetectionResult.Partial([], "php --version timed out"))
        };

        var scanner = new RuntimeDiscoveryScanner(detectors, NullLogger<RuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_DetectorThrows_IsIsolatedNeverFailsOverallScan()
    {
        var detectors = new IRuntimeDetector[]
        {
            new ThrowingRuntimeDetector(),
            new FakeRuntimeDetector("d1", "DotNetRuntime", RuntimeDetectionResult.Detected([MakeRow("DotNetRuntime")]))
        };

        var scanner = new RuntimeDiscoveryScanner(detectors, NullLogger<RuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.Single(result.Entities); // the working detector's result is kept
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_NoDetectorsRegistered_ReturnsSupportedWithEmptyEntities()
    {
        var scanner = new RuntimeDiscoveryScanner([], NullLogger<RuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
