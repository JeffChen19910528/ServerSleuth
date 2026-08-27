using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Windows.Software;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Software;

public class WindowsInstalledSoftwareScannerTests
{
    [Fact]
    public void BuildEntity_LocalMachine64Source_TagsX64ArchitectureAndRegistryEvidence()
    {
        var row = new SoftwareRegistryRow { RegistryKeyName = "{GUID}", DisplayName = "Contoso ERP Client" };

        var entity = WindowsInstalledSoftwareScanner.BuildEntity(row, SoftwareRegistrySource.LocalMachine64);

        Assert.Equal(EntityArchitecture.X64, entity.Architecture);
        Assert.Equal(EntityStatus.Installed, entity.Status);
        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.Registry);
    }

    [Fact]
    public void BuildEntity_Wow6432NodeSource_TagsX86Architecture()
    {
        var row = new SoftwareRegistryRow { RegistryKeyName = "{GUID}", DisplayName = "Contoso ERP Client (x86)" };

        var entity = WindowsInstalledSoftwareScanner.BuildEntity(row, SoftwareRegistrySource.LocalMachine32);

        Assert.Equal(EntityArchitecture.X86, entity.Architecture);
    }

    [Fact]
    public void BuildEntity_SameNameFromTwoSources_ProducesDistinctIds()
    {
        var row = new SoftwareRegistryRow { RegistryKeyName = "{GUID}", DisplayName = "Contoso ERP Client" };

        var entity64 = WindowsInstalledSoftwareScanner.BuildEntity(row, SoftwareRegistrySource.LocalMachine64);
        var entity32 = WindowsInstalledSoftwareScanner.BuildEntity(row, SoftwareRegistrySource.LocalMachine32);

        Assert.NotEqual(entity64.Id, entity32.Id); // scanner never merges across sources — that is Phase 5's job
    }

    [Fact]
    public async Task ScanAsync_AggregatesAcrossAllThreeSourcesWithoutMerging()
    {
        var reader = new FakeWindowsRegistryReader();

        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, SoftwareRegistrySource.LocalMachine64.Path, "{A}");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $"{SoftwareRegistrySource.LocalMachine64.Path}\\{{A}}",
            new Dictionary<string, object?> { ["DisplayName"] = "App A (64-bit)" });

        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, SoftwareRegistrySource.LocalMachine32.Path, "{B}");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry32, $"{SoftwareRegistrySource.LocalMachine32.Path}\\{{B}}",
            new Dictionary<string, object?> { ["DisplayName"] = "App B (32-bit)" });

        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, SoftwareRegistrySource.CurrentUser.Path); // empty

        var scanner = new WindowsInstalledSoftwareScanner(reader, NullLogger<WindowsInstalledSoftwareScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_OneSourceAccessDenied_ReturnsPartiallySupported()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, SoftwareRegistrySource.LocalMachine64.Path); // empty but accessible
        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, SoftwareRegistrySource.CurrentUser.Path); // empty but accessible
        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry32, SoftwareRegistrySource.LocalMachine32.Path);

        var scanner = new WindowsInstalledSoftwareScanner(reader, NullLogger<WindowsInstalledSoftwareScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
    }

    [Fact]
    public async Task ScanAsync_SkipsEntriesWithoutDisplayName()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, SoftwareRegistrySource.LocalMachine64.Path, "{PatchOnly}");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $"{SoftwareRegistrySource.LocalMachine64.Path}\\{{PatchOnly}}",
            new Dictionary<string, object?> { ["ReleaseType"] = "Security Update" }); // no DisplayName
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, SoftwareRegistrySource.LocalMachine32.Path);
        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, SoftwareRegistrySource.CurrentUser.Path);

        var scanner = new WindowsInstalledSoftwareScanner(reader, NullLogger<WindowsInstalledSoftwareScanner>.Instance);
        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Empty(result.Entities);
    }
}
