using System.Security.Cryptography;
using System.Text.Json;
using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>Manifest / integrity — see skill.md (Phase 9C) §11-12. Opt-in, safe-metadata-only,
/// SHA-256 verified against the exact bytes written.</summary>
public class LocalFileReportExporterManifestTests
{
    [Fact]
    public void Manifest_IsNotWrittenByDefault()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path);

        Assert.Null(result.Manifest);
        Assert.False(File.Exists(Path.Combine(temp.Path, "report-manifest.json")));
    }

    [Fact]
    public void Manifest_WrittenWhenExplicitlyRequested_ContainsBothArtifacts()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        var result = new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true);

        Assert.NotNull(result.Manifest);
        Assert.True(result.Manifest!.Success);
        var manifestPath = Path.Combine(temp.Path, "report-manifest.json");
        Assert.True(File.Exists(manifestPath));

        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var artifacts = doc.RootElement.GetProperty("Artifacts").EnumerateArray().ToList();
        Assert.Equal(2, artifacts.Count);
    }

    [Fact]
    public void Manifest_Sha256_MatchesActualWrittenFileBytes()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true);

        var manifestJson = File.ReadAllText(Path.Combine(temp.Path, "report-manifest.json"));
        using var doc = JsonDocument.Parse(manifestJson);

        foreach (var entry in doc.RootElement.GetProperty("Artifacts").EnumerateArray())
        {
            var fileName = entry.GetProperty("FileName").GetString()!;
            var expectedHash = entry.GetProperty("Sha256").GetString()!;
            var expectedLength = entry.GetProperty("ContentLength").GetInt64();

            var bytes = File.ReadAllBytes(Path.Combine(temp.Path, fileName));
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            Assert.Equal(expectedHash, actualHash);
            Assert.Equal(expectedLength, bytes.LongLength);
        }
    }

    [Fact]
    public void Manifest_HasNoCreatedAt_WhenNotSupplied()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true);

        var manifestJson = File.ReadAllText(Path.Combine(temp.Path, "report-manifest.json"));
        using var doc = JsonDocument.Parse(manifestJson);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, doc.RootElement.GetProperty("CreatedAt").ValueKind);
    }

    [Fact]
    public void Manifest_IncludesCreatedAt_OnlyWhenExplicitlySupplied()
    {
        using var temp = new TempDirectory();
        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var fixedTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true, manifestCreatedAt: fixedTime);

        var manifestJson = File.ReadAllText(Path.Combine(temp.Path, "report-manifest.json"));
        Assert.Contains("2026-01-01", manifestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_NeverContainsSecretOrRawConfigurationContent()
    {
        using var temp = new TempDirectory();
        var app = EntityFactory.Application("SecretApp", "/", @"D:\SecretApp");
        var config = EntityFactory.Configuration(@"D:\SecretApp\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Sensitive", "Password=DeliberatelySensitiveManifestValue123");

        var entities = new List<Core.Models.DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var bundle = ReportArtifactFactory.CreateBundle(report);

        new LocalFileReportExporter().ExportBundle(bundle, temp.Path, includeManifest: true);

        var manifestJson = File.ReadAllText(Path.Combine(temp.Path, "report-manifest.json"));
        Assert.DoesNotContain("DeliberatelySensitiveManifestValue123", manifestJson, StringComparison.Ordinal);
    }
}
