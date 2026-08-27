using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>Security regression for the export layer — see skill.md (Phase 9C) §22. Confirms
/// that deliberately-sensitive values injected into upstream entity Metadata never reach the
/// exported files on disk, and that no raw configuration content is exported.</summary>
public class LocalFileExportSecuritySafetyTests
{
    [Theory]
    [InlineData("Password=DeliberatelySensitiveExport1")]
    [InlineData("API_KEY=DeliberatelySensitiveExport2")]
    [InlineData("Bearer DeliberatelySensitiveExport3")]
    [InlineData("DB_PASSWORD=DeliberatelySensitiveExport4")]
    [InlineData("-----BEGIN PRIVATE KEY-----DeliberatelySensitiveExport5-----END PRIVATE KEY-----")]
    public void ExportedFiles_NeverContainDeliberatelyInjectedSensitiveValues(string sensitiveValue)
    {
        using var temp = new TempDirectory();
        var app = EntityFactory.Application("SecretExportApp", "/", @"D:\SecretExportApp");
        var config = EntityFactory.Configuration(@"D:\SecretExportApp\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "SecretDb");
        config.SetMetadata("Sensitive", sensitiveValue);

        var entities = new List<Core.Models.DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true);
        Assert.True(result.Success);

        var jsonBytes = System.IO.File.ReadAllText(result.Json.OutputPath!);
        var htmlBytes = System.IO.File.ReadAllText(result.Html.OutputPath!);
        var manifestBytes = System.IO.File.ReadAllText(result.Manifest!.OutputPath!);

        Assert.DoesNotContain(sensitiveValue, jsonBytes, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, htmlBytes, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, manifestBytes, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportedFiles_NeverContainRawXmlConfigurationMarkup()
    {
        using var temp = new TempDirectory();
        var app = EntityFactory.Application("XmlExportApp", "/", @"D:\XmlExportApp");
        var config = EntityFactory.Configuration(@"D:\XmlExportApp\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["FileShare: \\\\FILESERVER\\Share"]);

        var entities = new List<Core.Models.DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);
        Assert.True(result.Success);

        var jsonContent = System.IO.File.ReadAllText(result.Json.OutputPath!);
        var htmlContent = System.IO.File.ReadAllText(result.Html.OutputPath!);

        foreach (var content in new[] { jsonContent, htmlContent })
        {
            Assert.DoesNotContain("<configuration>", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("system.webServer", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connectionStrings", content, StringComparison.Ordinal);
        }
    }
}
