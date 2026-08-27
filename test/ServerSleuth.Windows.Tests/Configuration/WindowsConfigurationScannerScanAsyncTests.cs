using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.Configuration;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Tests.Fakes;
using ServerSleuth.Windows.Tests.IIS;
using ServerSleuth.Windows.Tests.ScheduledTasks;

namespace ServerSleuth.Windows.Tests.Configuration;

/// <summary>
/// Exercises WindowsConfigurationScanner.ScanAsync end-to-end against real temp files (real
/// encoding detection, real size limits) while using fakes only to control which single
/// directory becomes the scan root — so this is deterministic, not a real-machine test.
/// </summary>
public class WindowsConfigurationScannerScanAsyncTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemReader _fileSystemReader = new();

    public WindowsConfigurationScannerScanAsyncTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "serversleuth-config-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private WindowsConfigurationScanner MakeScanner()
    {
        var iisSnapshot = new IisSnapshot
        {
            Sites = [new IisSiteRow { Name = "TestSite", SiteId = 1, State = "Started", PhysicalPath = _tempDir }]
        };
        var iisScanner = new IisScanner(new FakeIisConfigurationProvider(IisProbeResult.Available(iisSnapshot)), _fileSystemReader, NullLogger<IisScanner>.Instance);

        var serviceScanner = new WindowsServiceScanner(new EmptyServiceEnumerator(), new FakeWindowsRegistryReader(), NullLogger<WindowsServiceScanner>.Instance);
        var taskScanner = new WindowsScheduledTaskScanner(new FakeTaskSchedulerProvider(TaskSchedulerProbeResult.Available([])), _fileSystemReader, new SecretRedactor(), NullLogger<WindowsScheduledTaskScanner>.Instance);

        return new WindowsConfigurationScanner(iisScanner, serviceScanner, taskScanner, _fileSystemReader, new SecretRedactor(), NullLogger<WindowsConfigurationScanner>.Instance);
    }

    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Deep, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task ScanAsync_JsonWithSecret_DetectsFileAndFlagsSecretWithoutExposingValue()
    {
        var path = Path.Combine(_tempDir, "appsettings.json");
        await File.WriteAllTextAsync(path, /*lang=json,strict*/ """{ "connectionStrings": { "Default": "Server=db01;Initial Catalog=ErpDb;Password=hunter2;" } }""");

        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Configuration>());
        Assert.True(entity.SecretDetected);
        Assert.DoesNotContain(entity.Metadata.Values, v => v.Contains("hunter2"));
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("SqlServer"));
    }

    [Fact]
    public async Task ScanAsync_Utf8BomFile_IsReadCorrectly()
    {
        var path = Path.Combine(_tempDir, "appsettings.json");
        await File.WriteAllTextAsync(path, """{ "logLevel": "Information" }""", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Configuration>());
        Assert.Equal("Parsed", entity.Metadata["ParseStatus"]);
    }

    [Fact]
    public async Task ScanAsync_Utf16File_IsReadCorrectly()
    {
        var path = Path.Combine(_tempDir, "appsettings.json");
        await File.WriteAllTextAsync(path, """{ "logLevel": "Information" }""", Encoding.Unicode);

        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Configuration>());
        Assert.Equal("Parsed", entity.Metadata["ParseStatus"]);
    }

    [Fact]
    public async Task ScanAsync_FileLargerThanOneMegabyte_IsSkippedNotFullyRead()
    {
        var path = Path.Combine(_tempDir, "huge.json");
        await File.WriteAllTextAsync(path, new string('x', 2 * 1024 * 1024));

        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Configuration>());
        Assert.Equal("SkippedTooLarge", entity.Metadata["ParseStatus"]);
        Assert.False(entity.SecretDetected); // never analyzed, since it was never read
    }

    [Fact]
    public async Task ScanAsync_MalformedJson_IsPartiallyParsedButStillAnalyzed()
    {
        var path = Path.Combine(_tempDir, "broken.json");
        await File.WriteAllTextAsync(path, "{ \"password\": \"hunter2\", "); // truncated/invalid

        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Configuration>());
        Assert.Equal("PartiallyParsed", entity.Metadata["ParseStatus"]);
        Assert.True(entity.SecretDetected); // text-based analysis still ran despite invalid JSON
    }

    [Fact]
    public async Task ScanAsync_MultipleConfigFiles_AreAllDiscoveredAndDeduplicated()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "web.config"), "<configuration/>");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "appsettings.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "sub", "values.yaml"), "key: value");

        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        Assert.Equal(3, result.Entities.Count);
        Assert.Equal(3, result.Entities.Select(e => e.Path).Distinct().Count());
    }

    [Fact]
    public async Task ScanAsync_NoConfigFilesInScanRoot_ReturnsSupportedWithEmptyEntities()
    {
        var result = await MakeScanner().ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }

    private sealed class EmptyServiceEnumerator : IServiceEnumerator
    {
        public IReadOnlyList<ServiceSnapshot> GetSnapshots() => [];
    }
}
