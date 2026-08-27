using ServerSleuth.Core.Evidence;
using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Tests.Configuration;

public class WindowsConfigurationScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    private static ScanRoot MakeRoot(string ownerId = "iis-site:ERP") => new()
    {
        Path = @"D:\Web\ERP",
        Source = "IIS",
        OwnerEntityId = ownerId,
        Reason = "IIS Site PhysicalPath",
        Confidence = Confidence.VeryHigh()
    };

    private static ConfigurationFileRow MakeRow(
        ConfigurationParseStatus status = ConfigurationParseStatus.Parsed,
        ConfigurationAnalysisResult? analysis = null) => new()
    {
        Path = @"D:\Web\ERP\appsettings.json",
        FileName = "appsettings.json",
        Format = ConfigurationFormat.Json,
        ParseStatus = status,
        SizeBytes = 512,
        OwnerEntityId = "iis-site:ERP",
        ScanRoot = MakeRoot(),
        Analysis = analysis
    };

    [Fact]
    public void BuildEntity_RecordsPathFormatAndOwner()
    {
        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(MakeRow(), Redactor);

        Assert.Equal(@"D:\Web\ERP\appsettings.json", entity.Path);
        Assert.Equal("Json", entity.Format);
        Assert.Equal("iis-site:ERP", entity.Metadata["OwnerEntityId"]);
    }

    [Fact]
    public void BuildEntity_NeverStoresRawFileContent()
    {
        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(MakeRow(), Redactor);

        Assert.DoesNotContain(entity.Metadata.Values, v => v.Length > 2000); // no raw file dump
    }

    [Fact]
    public void BuildEntity_SkippedTooLarge_StillRecordsSizeAndPath()
    {
        var row = MakeRow(status: ConfigurationParseStatus.SkippedTooLarge) with { SizeBytes = 5_000_000 };

        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal("SkippedTooLarge", entity.Metadata["ParseStatus"]);
        Assert.Equal("5000000", entity.Metadata["SizeBytes"]);
        Assert.Equal(@"D:\Web\ERP\appsettings.json", entity.Path);
    }

    [Fact]
    public void BuildEntity_AccessDenied_StillProducesEntityNotDropped()
    {
        var row = MakeRow(status: ConfigurationParseStatus.AccessDenied) with { Analysis = null, SizeBytes = null };

        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal("AccessDenied", entity.Metadata["ParseStatus"]);
        Assert.False(entity.SecretDetected);
    }

    [Fact]
    public void BuildEntity_AnalysisWithSecret_SetsSecretDetectedTrue()
    {
        var analysis = new ConfigurationAnalysisResult { SecretDetected = true };
        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(MakeRow(analysis: analysis), Redactor);

        Assert.True(entity.SecretDetected);
    }

    [Fact]
    public void BuildEntity_DependencyReferences_SummarizeEndpointsDatabasesAndUncPaths()
    {
        var analysis = new ConfigurationAnalysisResult
        {
            ExternalEndpoints = [new ExternalEndpointReference { Scheme = "https", Host = "api.company.com" }],
            DatabaseReferences = [new DatabaseReference { Type = "SqlServer", Host = "db01" }],
            NetworkPaths = [new UncPathReference { Server = "FILESERVER01", Share = "ERPData" }],
            RuntimeReferences = ["Java"],
            EnvironmentVariableReferences = ["JAVA_HOME"]
        };

        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(MakeRow(analysis: analysis), Redactor);

        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("api.company.com"));
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("SqlServer"));
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("FILESERVER01"));
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("Java"));
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("JAVA_HOME"));
    }

    [Fact]
    public void BuildEntity_RecordsFileSystemEvidence()
    {
        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(MakeRow(), Redactor);

        Assert.Contains(entity.Evidence, e => e.Type == Core.Enums.EvidenceType.ConfigurationFile && e.Location == @"D:\Web\ERP\appsettings.json");
    }

    [Fact]
    public void BuildEntity_NoOwnerEntityId_OmitsMetadataKey()
    {
        var row = MakeRow() with { OwnerEntityId = null };

        var entity = ServerSleuth.Windows.Configuration.WindowsConfigurationScanner.BuildEntity(row, Redactor);

        Assert.False(entity.Metadata.ContainsKey("OwnerEntityId"));
    }
}
