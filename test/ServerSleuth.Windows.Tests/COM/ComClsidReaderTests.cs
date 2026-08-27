using Microsoft.Win32;
using ServerSleuth.Windows.COM;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.COM;

public class ComClsidReaderTests
{
    private const string ClsidRoot = @"SOFTWARE\Classes\CLSID";
    private const string Clsid = "{11111111-2222-3333-4444-555555555555}";

    [Fact]
    public void Read_FullRegistration_MapsAllChildKeys()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}",
            new Dictionary<string, object?> { [""] = "Acme PDF Generator Class" });
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}",
            "ProgID", "InprocServer32", "TypeLib", "Version");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}\ProgID",
            new Dictionary<string, object?> { [""] = "Acme.PdfGenerator" });
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}\InprocServer32",
            new Dictionary<string, object?> { [""] = @"C:\Vendor\PdfGen.dll", ["ThreadingModel"] = "Both" });
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}\TypeLib",
            new Dictionary<string, object?> { [""] = "{99999999-8888-7777-6666-555555555555}" });
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}\Version",
            new Dictionary<string, object?> { [""] = "5.2" });

        var result = ComClsidReader.Read(reader, RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, Clsid);

        Assert.True(result.Success);
        var row = result.Row!;
        Assert.Equal("Acme PDF Generator Class", row.Name);
        Assert.Equal("Acme.PdfGenerator", row.ProgId);
        Assert.Equal(@"C:\Vendor\PdfGen.dll", row.InprocServer32!.ExecutablePath);
        Assert.Equal("Both", row.ThreadingModel);
        Assert.Equal("{99999999-8888-7777-6666-555555555555}", row.TypeLibClsid);
        Assert.Equal("5.2", row.VersionValue);
        Assert.Null(row.LocalServer32);
    }

    [Fact]
    public void Read_NoChildKeysPresent_ReturnsRowWithOnlyOwnName()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}",
            new Dictionary<string, object?> { [""] = "Minimal Class" });
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}");

        var result = ComClsidReader.Read(reader, RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, Clsid);

        Assert.True(result.Success);
        Assert.Equal("Minimal Class", result.Row!.Name);
        Assert.Null(result.Row.ProgId);
        Assert.Null(result.Row.InprocServer32);
        Assert.Null(result.Row.LocalServer32);
    }

    [Fact]
    public void Read_DoesNotQueryChildKeysThatAreNotListed()
    {
        // No SetValues configured for InprocServer32/LocalServer32/ProgID/TypeLib/Version —
        // if the reader queried them anyway, FakeWindowsRegistryReader would return NotFound
        // (not throw), so this also proves the "only read what's necessary" behavior doesn't
        // depend on those calls silently no-op-ing.
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}", new Dictionary<string, object?>());
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}");

        var result = ComClsidReader.Read(reader, RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, Clsid);

        Assert.True(result.Success);
        Assert.Null(result.Row!.ProgId);
    }

    [Fact]
    public void Read_LocalServer32WithQuotedArguments_ParsesPathAndArguments()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}", new Dictionary<string, object?>());
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}", "LocalServer32");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}\LocalServer32",
            new Dictionary<string, object?> { [""] = @"""C:\Vendor\Server.exe"" /automation" });

        var result = ComClsidReader.Read(reader, RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, Clsid);

        Assert.Equal(@"C:\Vendor\Server.exe", result.Row!.LocalServer32!.ExecutablePath);
        Assert.Equal("/automation", result.Row.LocalServer32.Arguments);
    }

    [Fact]
    public void Read_AccessDeniedOnOwnValues_ReturnsFailureNotException()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetAccessDenied(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{ClsidRoot}\{Clsid}");

        var result = ComClsidReader.Read(reader, RegistryHive.LocalMachine, RegistryView.Registry64, ClsidRoot, Clsid);

        Assert.False(result.Success);
        Assert.Contains("access denied", result.FailureReason);
    }
}
