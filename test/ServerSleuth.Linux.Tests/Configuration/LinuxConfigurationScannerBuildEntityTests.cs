using ServerSleuth.Core.Evidence;
using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Configuration;

namespace ServerSleuth.Linux.Tests.Configuration;

public class LinuxConfigurationScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    private static ScanRoot MakeRoot(string source = "ApplicationRoot", string ownerId = "service:erp") => new()
    {
        Path = "/opt/erp",
        Source = source,
        OwnerEntityId = ownerId,
        Reason = "test",
        Confidence = Confidence.High()
    };

    [Fact]
    public void BuildEntity_ParsedFile_UsesScanRootConfidence()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/opt/erp/appsettings.json",
            FileName = "appsettings.json",
            Format = ConfigurationFormat.Json,
            ParseStatus = ConfigurationParseStatus.Parsed,
            ScanRoot = MakeRoot()
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal("configuration:/opt/erp/appsettings.json", entity.Id);
        Assert.Equal(Confidence.High(), entity.Confidence);
        Assert.Equal("Json", entity.Format);
    }

    [Fact]
    public void BuildEntity_OwnerEntityId_RecordedInMetadata()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/opt/erp/erp.env",
            FileName = "erp.env",
            Format = ConfigurationFormat.EnvFile,
            ParseStatus = ConfigurationParseStatus.Parsed,
            OwnerEntityId = "service:erp",
            ScanRoot = MakeRoot()
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal("service:erp", entity.Metadata["OwnerEntityId"]);
    }

    [Fact]
    public void BuildEntity_Symlink_RecordsIsSymlinkMetadata()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/etc/nginx/nginx.conf",
            FileName = "nginx.conf",
            Format = ConfigurationFormat.Unknown,
            ParseStatus = ConfigurationParseStatus.Unsupported,
            ScanRoot = MakeRoot("Nginx", null!),
            IsSymlink = true
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal("True", entity.Metadata["IsSymlink"]);
    }

    [Fact]
    public void BuildEntity_TechnologyFactsWithSecretShapedValue_AreRedacted()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/opt/erp/erp.service",
            FileName = "erp.service",
            Format = ConfigurationFormat.Unknown,
            ParseStatus = ConfigurationParseStatus.Parsed,
            ScanRoot = MakeRoot("Systemd"),
            TechnologyFacts = new Dictionary<string, string> { ["ExecStart"] = "/opt/erp/bin/erp --token=SuperSecretToken123" }
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.DoesNotContain("SuperSecretToken123", entity.Metadata["Systemd.ExecStart"]);
    }

    [Fact]
    public void BuildEntity_NetworkStorageAndUnixSocketReferences_RecordedInMetadata()
    {
        var analysis = new ConfigurationAnalysisResult
        {
            NetworkStorageReferences = [new NetworkStorageReference { Protocol = "NFS", Server = "nfs.internal", Path = "/exports/erp" }],
            UnixSocketReferences = ["/run/erp/app.sock"]
        };
        var row = new ConfigurationFileRow
        {
            Path = "/opt/erp/config.yml",
            FileName = "config.yml",
            Format = ConfigurationFormat.Yaml,
            ParseStatus = ConfigurationParseStatus.Parsed,
            ScanRoot = MakeRoot(),
            Analysis = analysis
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal("NFS", entity.Metadata["NetworkStorage0.Protocol"]);
        Assert.Equal("nfs.internal", entity.Metadata["NetworkStorage0.Server"]);
        Assert.Equal("/run/erp/app.sock", entity.Metadata["UnixSocket0"]);
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("NetworkStorage"));
        Assert.Contains(entity.DetectedDependencyReferences, r => r.Contains("UnixSocket"));
    }

    [Fact]
    public void BuildEntity_UnparsedFile_UsesMediumConfidence_NeverScanRootConfidence()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/opt/erp/broken.json",
            FileName = "broken.json",
            Format = ConfigurationFormat.Json,
            ParseStatus = ConfigurationParseStatus.PartiallyParsed,
            ScanRoot = MakeRoot()
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.Equal(Confidence.Medium(), entity.Confidence);
    }

    [Fact]
    public void BuildEntity_SecretDetectedInAnalysis_PropagatesToEntity()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/opt/erp/erp.env",
            FileName = "erp.env",
            Format = ConfigurationFormat.EnvFile,
            ParseStatus = ConfigurationParseStatus.Parsed,
            ScanRoot = MakeRoot(),
            Analysis = new ConfigurationAnalysisResult { SecretDetected = true }
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        Assert.True(entity.SecretDetected);
    }

    [Fact]
    public void BuildEntity_EvidenceRecordsPathAndScanRootSource_NeverFullFileContent()
    {
        var row = new ConfigurationFileRow
        {
            Path = "/etc/ssh/sshd_config",
            FileName = "sshd_config",
            Format = ConfigurationFormat.Unknown,
            ParseStatus = ConfigurationParseStatus.Unsupported,
            ScanRoot = MakeRoot("Ssh", null!)
        };

        var entity = LinuxConfigurationScanner.BuildEntity(row, Redactor);

        var evidence = Assert.Single(entity.Evidence);
        Assert.Equal("/etc/ssh/sshd_config", evidence.Location);
        Assert.Contains("Ssh", evidence.Detail);
    }
}
