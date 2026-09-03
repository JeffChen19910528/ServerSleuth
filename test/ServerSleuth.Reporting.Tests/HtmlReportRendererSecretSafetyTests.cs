using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Secret non-disclosure for the HTML renderer — see skill.md (Phase 9B) §16. Reuses Phase 9A's
/// <c>ReportDtoMapper</c>/DTO tree, so the same structural guarantee applies: neither the domain
/// model nor the DTO contract carries a raw secret/credential field, and Phase 8A/8B/8C never
/// carry entity <c>Metadata</c> forward into any Migration-layer model.
/// </summary>
public class HtmlReportRendererSecretSafetyTests
{
    [Theory]
    [InlineData("Password=Sw0rdfish-Sup3rSecret-9f8e7d6c5b4a")]
    [InlineData("API_KEY=sk-live-deliberately-sensitive-abc123")]
    [InlineData("Bearer deliberately.sensitive.jwt.token")]
    [InlineData("-----BEGIN PRIVATE KEY-----DELIBERATELYSENSITIVE-----END PRIVATE KEY-----")]
    [InlineData("DB_PASSWORD=deliberately-sensitive-db-secret")]
    public void DeliberatelySensitiveValue_StashedInEntityMetadata_NeverAppearsInRenderedHtml(string sensitiveValue)
    {
        var app = EntityFactory.Application("SecretApp", "/", @"D:\SecretApp");
        var config = EntityFactory.Configuration(@"D:\SecretApp\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "SecretDb");
        config.SetMetadata("Sensitive", sensitiveValue);

        var entities = new List<DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain(sensitiveValue, html, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationDependencyReferences_AreNeverRenderedRaw()
    {
        // The Server Deployment Inventory report never renders a Configuration entity's detected
        // dependency references (only its file name, when linked to an Application) — internal
        // evidence/detail text must not leak into the simplified report.
        var config = EntityFactory.Configuration(@"D:\Redacted\web.config", dependencyReferences: ["FileShare: [REDACTED]"]);
        var entities = new List<DiscoveryEntity> { config };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FileShare:", html, StringComparison.Ordinal);
    }
}
