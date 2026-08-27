using Microsoft.Win32;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Runtimes;

public class DotNetFrameworkDetectorTests
{
    private const string V4Path = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
    private const string V35Path = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5";

    [Theory]
    [InlineData(528040, "4.8")]
    [InlineData(533320, "4.8.1")]
    [InlineData(461808, "4.7.2")]
    [InlineData(378389, "4.5")]
    public void MapRelease_KnownThresholds_ReturnCorrectVersion(int release, string expected)
    {
        Assert.Equal(expected, DotNetFrameworkDetector.MapRelease(release));
    }

    [Fact]
    public void MapRelease_BelowLowestThreshold_ReturnsNull()
    {
        Assert.Null(DotNetFrameworkDetector.MapRelease(100000));
    }

    [Fact]
    public async Task DetectAsync_KnownRelease_MapsToVersion()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V4Path,
            new Dictionary<string, object?> { ["Release"] = 528040, ["Version"] = "4.8.03761" });

        var detector = new DotNetFrameworkDetector(reader);
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("4.8", row.Version);
        Assert.Null(row.ConflictNote);
    }

    [Fact]
    public async Task DetectAsync_ReleaseBelowLowestKnownThreshold_FallsBackToRawVersionWithConflictNote()
    {
        // A Release value below our lowest documented threshold (e.g. a very early / unusual
        // installation) must never be forced into a guessed version bucket.
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V4Path,
            new Dictionary<string, object?> { ["Release"] = 300000, ["Version"] = "4.0.0" });

        var detector = new DotNetFrameworkDetector(reader);
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("4.0.0", row.Version); // raw registry value, never guessed
        Assert.NotNull(row.ConflictNote);
    }

    [Fact]
    public async Task DetectAsync_ReleaseAboveHighestThreshold_MapsToFinalKnownVersion()
    {
        // .NET Framework 4.8.1 is the final version ever released in that product line, so a
        // Release value at or beyond its threshold correctly maps to it — this is Microsoft's
        // own documented ">=" methodology, not a guess.
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V4Path,
            new Dictionary<string, object?> { ["Release"] = 999999, ["Version"] = "4.8.1" });

        var detector = new DotNetFrameworkDetector(reader);
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("4.8.1", row.Version);
        Assert.Null(row.ConflictNote);
    }

    [Fact]
    public async Task DetectAsync_V35Installed_IsReportedAsSeparateEntry()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V4Path,
            new Dictionary<string, object?> { ["Release"] = 528040 });
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V35Path,
            new Dictionary<string, object?> { ["Install"] = 1 });

        var detector = new DotNetFrameworkDetector(reader);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Version == "3.5");
        Assert.Contains(result.Rows, r => r.Version == "4.8");
    }

    [Fact]
    public async Task DetectAsync_NothingInstalled_ReturnsNotDetected()
    {
        var detector = new DotNetFrameworkDetector(new FakeWindowsRegistryReader());
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(Core.Enums.ScannerStatus.NotInstalled, result.Status);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task DetectAsync_V35KeyPresentButInstallZero_IsNotReported()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, V35Path,
            new Dictionary<string, object?> { ["Install"] = 0 });

        var detector = new DotNetFrameworkDetector(reader);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(Core.Enums.ScannerStatus.NotInstalled, result.Status);
    }
}
