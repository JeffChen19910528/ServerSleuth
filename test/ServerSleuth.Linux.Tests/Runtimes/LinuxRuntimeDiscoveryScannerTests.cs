using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Linux.Runtimes;

namespace ServerSleuth.Linux.Tests.Runtimes;

public class LinuxRuntimeDiscoveryScannerTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    private sealed class FakeDetector(string id, string family, RuntimeDetectionResult result) : IRuntimeDetector
    {
        public string Id => id;
        public string RuntimeFamily => family;
        public Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private static RuntimeDetectionRow Row(string family, string version) => new()
    {
        Family = family,
        EntityKind = RuntimeEntityKind.Runtime,
        Name = family,
        Version = version,
        ExecutablePath = $"/usr/bin/{family.ToLowerInvariant()}",
        ExecutableAvailable = true,
        DetectionSources = ["Command"],
        Command = $"{family} --version"
    };

    [Fact]
    public async Task ScanAsync_TwoDetectorsBothFindSomething_AggregatesBoth()
    {
        var detectors = new IRuntimeDetector[]
        {
            new FakeDetector("d1", "Python", RuntimeDetectionResult.Detected([Row("Python", "3.11.6")])),
            new FakeDetector("d2", "Go", RuntimeDetectionResult.Detected([Row("Go", "1.21.5")]))
        };

        var scanner = new LinuxRuntimeDiscoveryScanner(detectors, NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OneDetectorFails_OthersStillContribute_ReturnsPartiallySupported()
    {
        var detectors = new IRuntimeDetector[]
        {
            new FakeDetector("d1", "Python", RuntimeDetectionResult.Detected([Row("Python", "3.11.6")])),
            new FakeDetector("d2", "Go", RuntimeDetectionResult.Failure("boom"))
        };

        var scanner = new LinuxRuntimeDiscoveryScanner(detectors, NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_NoDetectorsFindAnything_ReturnsSupportedWithEmptyResult()
    {
        var detectors = new IRuntimeDetector[]
        {
            new FakeDetector("d1", "Python", RuntimeDetectionResult.NotDetected())
        };

        var scanner = new LinuxRuntimeDiscoveryScanner(detectors, NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);
        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
