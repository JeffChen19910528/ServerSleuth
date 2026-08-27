using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.COM;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.COM;

public class WindowsComScannerScanAsyncTests
{
    private const string ClsidRoot = @"SOFTWARE\Classes\CLSID";
    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Deep, CancellationToken = CancellationToken.None };

    private static WindowsComScanner MakeScanner(FakeWindowsRegistryReader reader) =>
        new(reader, new FileSystemReader(), new NullFileVersionMetadataReader(), new SecretRedactor(), NullLogger<WindowsComScanner>.Instance);

    private static void RegisterMinimalClsid(FakeWindowsRegistryReader reader, RegistryHive hive, RegistryView view, string clsid, string name = "Test Class")
    {
        reader.SetValues(hive, view, $@"{ClsidRoot}\{clsid}", new Dictionary<string, object?> { [""] = name });
        reader.SetSubKeyNames(hive, view, $@"{ClsidRoot}\{clsid}");
    }

    [Fact]
    public async Task ScanAsync_MultipleRegistrySources_AggregatesAllWithCorrectScopeAndView()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, "{A}");
        RegisterMinimalClsid(reader, RegistryHive.LocalMachine, RegistryView.Registry64, "{A}", "Machine64 Class");

        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot, "{B}");
        RegisterMinimalClsid(reader, RegistryHive.LocalMachine, RegistryView.Registry32, "{B}", "Machine32 Class");

        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot, "{C}");
        RegisterMinimalClsid(reader, RegistryHive.CurrentUser, RegistryView.Default, "{C}", "User Class");

        var result = await MakeScanner(reader).ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(3, result.Entities.Count);

        var entities = result.Entities.Cast<Core.Models.ComComponent>().ToList();
        var machine64 = entities.Single(e => e.Name == "Machine64 Class");
        Assert.Equal("Machine", machine64.Metadata["RegistrationScope"]);
        Assert.Equal("Registry64", machine64.Metadata["RegistryView"]);
        Assert.Contains(machine64.Evidence, e => e.Type == EvidenceType.Registry);

        var machine32 = entities.Single(e => e.Name == "Machine32 Class");
        Assert.Equal("Registry32", machine32.Metadata["RegistryView"]);

        var user = entities.Single(e => e.Name == "User Class");
        Assert.Equal("User", user.Metadata["RegistrationScope"]);
        Assert.Equal("Default", user.Metadata["RegistryView"]);
    }

    [Fact]
    public async Task ScanAsync_SameClsidInBothRegistryViews_ProducesTwoDistinctEntitiesNeitherDropped()
    {
        const string clsid = "{99999999-1111-2222-3333-444444444444}";
        var reader = new FakeWindowsRegistryReader();

        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, clsid);
        RegisterMinimalClsid(reader, RegistryHive.LocalMachine, RegistryView.Registry64, clsid, "Shared Class");

        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot, clsid);
        RegisterMinimalClsid(reader, RegistryHive.LocalMachine, RegistryView.Registry32, clsid, "Shared Class");

        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot); // empty

        var result = await MakeScanner(reader).ScanAsync(Context, CancellationToken.None);

        var entities = result.Entities.Cast<Core.Models.ComComponent>().ToList();
        Assert.Equal(2, entities.Count);
        Assert.All(entities, e => Assert.Equal(clsid.ToUpperInvariant(), e.Clsid));
        Assert.Equal(2, entities.Select(e => e.Id).Distinct().Count()); // distinct Ids, not merged
        Assert.Equal(2, entities.Select(e => e.Metadata["RegistryView"]).Distinct().Count()); // both views represented
    }

    [Fact]
    public async Task ScanAsync_OneClsidAccessDeniedAmongMany_KeepsSuccessfulOnesAndReportsPartial()
    {
        var reader = new FakeWindowsRegistryReader();
        var clsids = Enumerable.Range(1, 9).Select(i => $"{{00000000-0000-0000-0000-00000000000{i}}}").ToList();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, [.. clsids, "{DENIED}"]);

        foreach (var clsid in clsids)
        {
            RegisterMinimalClsid(reader, RegistryHive.LocalMachine, RegistryView.Registry64, clsid);
        }

        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{{DENIED}}");
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot);
        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);

        var result = await MakeScanner(reader).ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Equal(9, result.Entities.Count);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_OneWholeSourceAccessDenied_KeepsOtherSourcesAndReportsPartial()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot);

        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot, "{A}");
        RegisterMinimalClsid(reader, RegistryHive.LocalMachine, RegistryView.Registry32, "{A}");

        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);

        var result = await MakeScanner(reader).ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
        Assert.Contains(result.Errors, e => e.Message.Contains("Registry64") || e.Message.Contains(@"HKLM\SOFTWARE\Classes\CLSID"));
    }

    [Fact]
    public async Task ScanAsync_AllSourcesAccessDenied_ReturnsAccessDeniedNotEmptyOrNotInstalled()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot);
        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot);
        reader.SetAccessDenied(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);

        var result = await MakeScanner(reader).ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.AccessDenied, result.Status);
        Assert.Empty(result.Entities);
        Assert.NotEqual(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task ScanAsync_AllSourcesAccessibleButEmpty_ReturnsSupportedNotNotInstalled()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot);
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry32, ClsidRoot);
        reader.SetSubKeyNames(RegistryHive.CurrentUser, RegistryView.Default, ClsidRoot);

        var result = await MakeScanner(reader).ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
