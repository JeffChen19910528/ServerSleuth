using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.COM;

namespace ServerSleuth.Windows.Tests.COM;

public class WindowsComScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    private static ComClsidRow MakeRow(
        string clsid = "{11111111-2222-3333-4444-555555555555}",
        string? progId = "Acme.PdfGenerator",
        ServerReference? inproc = null,
        ServerReference? local = null,
        string? threadingModel = "Both") => new()
    {
        Clsid = clsid,
        Name = "Acme PDF Generator Class",
        ProgId = progId,
        InprocServer32 = inproc,
        ThreadingModel = threadingModel,
        LocalServer32 = local,
        TypeLibClsid = "{99999999-8888-7777-6666-555555555555}",
        VersionValue = "5.2"
    };

    [Fact]
    public void BuildEntity_InprocOnly_ReportsInProcessServerType()
    {
        var row = MakeRow(inproc: ServerReference.Parse(@"C:\Vendor\PdfGen.dll"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("InProcess", entity.Metadata["ServerType"]);
        Assert.Equal(@"C:\Vendor\PdfGen.dll", entity.InprocServer32);
        Assert.Equal(@"C:\Vendor\PdfGen.dll", entity.Path);
        Assert.Null(entity.LocalServer32);
    }

    [Fact]
    public void BuildEntity_LocalServerOnly_ReportsLocalServerType()
    {
        var row = MakeRow(inproc: null, local: ServerReference.Parse(@"C:\Vendor\Server.exe"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("LocalServer", entity.Metadata["ServerType"]);
        Assert.Equal(@"C:\Vendor\Server.exe", entity.LocalServer32);
    }

    [Fact]
    public void BuildEntity_BothServersPresent_ReportsBoth()
    {
        var row = MakeRow(inproc: ServerReference.Parse(@"C:\Vendor\PdfGen.dll"), local: ServerReference.Parse(@"C:\Vendor\Server.exe"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("Both", entity.Metadata["ServerType"]);
    }

    [Fact]
    public void BuildEntity_NeitherServerPresent_ReportsUnknown()
    {
        var row = MakeRow(inproc: null, local: null);

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("Unknown", entity.Metadata["ServerType"]);
        Assert.Null(entity.Path);
    }

    [Fact]
    public void BuildEntity_NameFallsBackFromProgIdToRegistryNameToClsid()
    {
        var withProgId = WindowsComScanner.BuildEntity(MakeRow(progId: "Acme.PdfGenerator"), ComRegistrationSource.LocalMachine64, Redactor);
        Assert.Equal("Acme.PdfGenerator", withProgId.Name);

        var row = MakeRow(progId: null) with { Name = "Fallback Name" };
        var withoutProgId = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);
        Assert.Equal("Fallback Name", withoutProgId.Name);

        var bareRow = MakeRow(progId: null) with { Name = null };
        var bare = WindowsComScanner.BuildEntity(bareRow, ComRegistrationSource.LocalMachine64, Redactor);
        Assert.Equal(bareRow.Clsid.ToUpperInvariant(), bare.Name);
    }

    [Fact]
    public void BuildEntity_Clsid_IsCanonicalizedToUppercase()
    {
        var row = MakeRow(clsid: "{11111111-2222-3333-4444-555555555555}".ToLowerInvariant());

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("{11111111-2222-3333-4444-555555555555}", entity.Clsid);
    }

    [Fact]
    public void BuildEntity_StatusIsInstalledNotUsed_MatchingRegisteredNotUsedSemantics()
    {
        var entity = WindowsComScanner.BuildEntity(MakeRow(), ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal(EntityStatus.Installed, entity.Status);
        Assert.Equal("Registered", entity.Metadata["RegistrationStatus"]);
    }

    [Theory]
    [InlineData("LocalMachine64", "Machine", "Registry64")]
    [InlineData("LocalMachine32", "Machine", "Registry32")]
    [InlineData("CurrentUser", "User", "Default")]
    public void BuildEntity_RecordsRegistrationScopeAndRegistryView(string sourceName, string expectedScope, string expectedView)
    {
        var source = sourceName switch
        {
            "LocalMachine64" => ComRegistrationSource.LocalMachine64,
            "LocalMachine32" => ComRegistrationSource.LocalMachine32,
            _ => ComRegistrationSource.CurrentUser
        };

        var entity = WindowsComScanner.BuildEntity(MakeRow(), source, Redactor);

        Assert.Equal(expectedScope, entity.Metadata["RegistrationScope"]);
        Assert.Equal(expectedView, entity.Metadata["RegistryView"]);
    }

    [Fact]
    public void BuildEntity_SameClsidDifferentRegistryViews_ProducesDistinctIds()
    {
        var row = MakeRow();

        var entity64 = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);
        var entity32 = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine32, Redactor);

        Assert.NotEqual(entity64.Id, entity32.Id); // never merged here — Phase 5's job.
        Assert.Equal(entity64.Clsid, entity32.Clsid); // but the logical CLSID identity is preserved.
    }

    [Fact]
    public void BuildEntity_AmbiguousUnquotedLocalServer32_RecordsRawReferenceDetectedNotAGuess()
    {
        var row = MakeRow(local: ServerReference.Parse(@"C:\Program Files\Vendor\Server.exe"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Null(entity.LocalServer32);
        Assert.Equal("RawReferenceDetected", entity.Metadata["LocalServer32Status"]);
        Assert.Equal(@"C:\Program Files\Vendor\Server.exe", entity.Metadata["LocalServer32RawValue"]);
    }

    [Fact]
    public void BuildEntity_LocalServer32ArgumentsContainingSecret_AreRedactedAndFlagged()
    {
        var row = MakeRow(local: ServerReference.Parse(@"""C:\Vendor\Server.exe"" --ApiKey=sk-abc123secret"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.DoesNotContain("sk-abc123secret", entity.Metadata["LocalServer32Arguments"]);
        Assert.Contains("[REDACTED]", entity.Metadata["LocalServer32Arguments"]);
        Assert.Equal("true", entity.Metadata["SecretDetected"]);
    }

    [Fact]
    public void BuildEntity_LocalServer32ArgumentsWithoutSecret_AreNotFlagged()
    {
        var row = MakeRow(local: ServerReference.Parse(@"""C:\Vendor\Server.exe"" /automation"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("/automation", entity.Metadata["LocalServer32Arguments"]);
        Assert.False(entity.Metadata.ContainsKey("SecretDetected"));
    }

    [Fact]
    public void BuildEntity_NoFileSystemOrVersionReader_SkipsFileVerificationWithoutThrowing()
    {
        var row = MakeRow(inproc: ServerReference.Parse(@"C:\Vendor\PdfGen.dll"));

        var entity = WindowsComScanner.BuildEntity(row, ComRegistrationSource.LocalMachine64, Redactor);

        Assert.False(entity.Metadata.ContainsKey("ServerPathStatus"));
        Assert.False(entity.Metadata.ContainsKey("ServerFileSizeBytes"));
    }

    [Fact]
    public void BuildEntity_RegistryVersionValue_IsUsedAsEntityVersion()
    {
        var entity = WindowsComScanner.BuildEntity(MakeRow(), ComRegistrationSource.LocalMachine64, Redactor);

        Assert.Equal("5.2", entity.Version);
    }
}
