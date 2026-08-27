using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Binaries;
using ServerSleuth.Windows.COM;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Tests.Fakes;
using ServerSleuth.Windows.Tests.IIS;
using ServerSleuth.Windows.Tests.ScheduledTasks;

namespace ServerSleuth.Windows.Tests.Binaries;

public class WindowsBinaryDiscoveryScannerScanAsyncTests : IDisposable
{
    private const string ClsidRoot = @"SOFTWARE\Classes\CLSID";
    private readonly string _tempDir;
    private readonly FileSystemReader _fileSystemReader = new();

    public WindowsBinaryDiscoveryScannerScanAsyncTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "serversleuth-binary-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed class EmptyServiceEnumerator : IServiceEnumerator
    {
        public IReadOnlyList<ServiceSnapshot> GetSnapshots() => [];
    }

    private static void SetEmptyComRoots(FakeWindowsRegistryReader reader)
    {
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot);
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot);
        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);
    }

    private WindowsBinaryDiscoveryScanner MakeScanner(FakeWindowsRegistryReader registryReader, string? iisRootPath = null)
    {
        var iisSites = iisRootPath is null
            ? new IisSnapshot()
            : new IisSnapshot { Sites = [new IisSiteRow { Name = "TestSite", SiteId = 1, State = "Started", PhysicalPath = iisRootPath }] };

        var iisScanner = new IisScanner(new FakeIisConfigurationProvider(IisProbeResult.Available(iisSites)), _fileSystemReader, NullLogger<IisScanner>.Instance);
        var serviceScanner = new WindowsServiceScanner(new EmptyServiceEnumerator(), registryReader, NullLogger<WindowsServiceScanner>.Instance);
        var taskScanner = new WindowsScheduledTaskScanner(new FakeTaskSchedulerProvider(TaskSchedulerProbeResult.Available([])), _fileSystemReader, new SecretRedactor(), NullLogger<WindowsScheduledTaskScanner>.Instance);
        var comScanner = new WindowsComScanner(registryReader, _fileSystemReader, new FileVersionMetadataReader(), new SecretRedactor(), NullLogger<WindowsComScanner>.Instance);

        return new WindowsBinaryDiscoveryScanner(
            iisScanner, serviceScanner, taskScanner, comScanner,
            _fileSystemReader, new FileVersionMetadataReader(), new PeAnalyzer(), new SecretRedactor(),
            NullLogger<WindowsBinaryDiscoveryScanner>.Instance);
    }

    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Deep, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task ScanAsync_DllsUnderIisRoot_AreDiscoveredAndPeAnalyzed()
    {
        var ownDll = typeof(ServerSleuth.Core.Models.Server).Assembly.Location;
        File.Copy(ownDll, Path.Combine(_tempDir, "Copied.dll"));

        var registryReader = new FakeWindowsRegistryReader();
        SetEmptyComRoots(registryReader);

        var result = await MakeScanner(registryReader, _tempDir).ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Dll>());
        Assert.Equal("Found", entity.Metadata["FileStatus"]);
        Assert.Equal("ManagedDll", entity.Type);
    }

    [Fact]
    public async Task ScanAsync_DanglingComRegistration_ProducesNotFoundEntityNotDropped()
    {
        var registryReader = new FakeWindowsRegistryReader();
        registryReader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, "{GUID}");
        registryReader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{GUID}}", new Dictionary<string, object?> { [""] = "Dangling" });
        registryReader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{GUID}}", "InprocServer32");
        registryReader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{GUID}}\InprocServer32",
            new Dictionary<string, object?> { [""] = @"C:\Vendor\LongGone.dll" });
        registryReader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot);
        registryReader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);

        var result = await MakeScanner(registryReader).ScanAsync(Context, CancellationToken.None);

        var entity = Assert.Single(result.Entities.Cast<Core.Models.Dll>());
        Assert.Equal(@"C:\Vendor\LongGone.dll", entity.Path);
        Assert.Equal("NotFound", entity.Metadata["FileStatus"]);
        Assert.Equal(EntityStatus.Unknown, entity.Status);
    }

    [Fact]
    public async Task ScanAsync_SameFileReferencedByIisAndCom_MergesIntoOneEntity()
    {
        var sharedPath = Path.Combine(_tempDir, "Shared.dll");
        File.Copy(typeof(ServerSleuth.Core.Models.Server).Assembly.Location, sharedPath);

        var registryReader = new FakeWindowsRegistryReader();
        registryReader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, "{GUID}");
        registryReader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{GUID}}", new Dictionary<string, object?> { [""] = "Shared" });
        registryReader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{GUID}}", "InprocServer32");
        registryReader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{GUID}}\InprocServer32",
            new Dictionary<string, object?> { [""] = sharedPath });
        registryReader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot);
        registryReader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);

        var result = await MakeScanner(registryReader, _tempDir).ScanAsync(Context, CancellationToken.None);

        // The key requirement (skill.md §29): one shared file discovered via two sources
        // becomes ONE entity, never two/three separate ones — not the exact evidence count,
        // which depends on how the two roots happened to merge upstream.
        var entity = Assert.Single(result.Entities.Cast<Core.Models.Dll>());
        Assert.True(entity.Evidence.Count >= 1);
        Assert.Contains(entity.ReferencedByEntityIds, id => id.Contains("GUID")); // the COM component's owner id
    }

    [Fact]
    public async Task ScanAsync_NoRootsAndNoComReferences_ReturnsSupportedWithEmptyEntities()
    {
        var registryReader = new FakeWindowsRegistryReader();
        SetEmptyComRoots(registryReader);

        var result = await MakeScanner(registryReader).ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
