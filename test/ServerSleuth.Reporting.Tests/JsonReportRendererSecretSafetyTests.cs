using System.Reflection;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Json.Dto;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Secret non-disclosure — see skill.md (Phase 9A) §6. The domain model (<c>Configuration</c>,
/// <c>ExternalDependency</c>) never carries a raw secret/credential field in the first place (see
/// skill.md §44 — enforced back in Phase 2/4E, not something Reporting re-implements), and Phase
/// 8A/8B/8C never carry entity <c>Metadata</c> (the dictionary a scanner might use to stash a
/// discovered connection string) forward into <c>MigrationIssue</c>/<c>MigrationDependency</c>/
/// <c>MigrationAction</c>/<c>MigrationVerificationCheck</c> at all. These tests prove that
/// structural guarantee end-to-end through the real renderer, and pin the DTO contract itself so
/// a future field addition can't quietly reopen the leak (skill.md §6: "solve it at the reporting
/// DTO/contract boundary").
/// </summary>
public class JsonReportRendererSecretSafetyTests
{
    private const string DeliberatelySensitiveValue = "Sw0rdfish-Sup3rSecret-9f8e7d6c5b4a";

    [Fact]
    public void ConnectionStringPasswordStashedInEntityMetadata_NeverAppearsInRenderedJson()
    {
        var app = EntityFactory.Application("SecretApp", "/", @"D:\SecretApp");
        var config = EntityFactory.Configuration(@"D:\SecretApp\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "SecretDb");
        // Deliberately constructed sensitive-looking input, per skill.md §6 — a scanner might
        // (incorrectly) stash a raw connection-string credential in entity Metadata; Reporting
        // must never surface it regardless, because Phase 8A/8B/8C never read Metadata at all.
        config.SetMetadata("Database0.ConnectionString", $"Server=DB01;User=sa;Password={DeliberatelySensitiveValue};");

        var entities = new List<DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var json = new JsonReportRenderer().Render(report).Content;

        Assert.DoesNotContain(DeliberatelySensitiveValue, json, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiKeyAndBearerTokenStashedInMetadata_NeverAppearInRenderedJson()
    {
        var service = EntityFactory.Service("TokenSvc", @"D:\Token\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Token\svc.exe", notFound: true);
        missingExe.SetMetadata("API_KEY", "sk-live-deliberately-sensitive-abc123");
        missingExe.SetMetadata("Authorization", "Bearer deliberately.sensitive.jwt.token");
        missingExe.SetMetadata("PRIVATE_KEY", "-----BEGIN PRIVATE KEY-----DELIBERATELYSENSITIVE-----END PRIVATE KEY-----");

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var json = new JsonReportRenderer().Render(report).Content;

        Assert.DoesNotContain("sk-live-deliberately-sensitive-abc123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("deliberately.sensitive.jwt.token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DELIBERATELYSENSITIVE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AlreadyRedactedEvidenceDetail_PassesThroughAsRedactedMarker_NeverReExposed()
    {
        // Simulates what the real upstream SecretRedactor (Phase 2) already produces: a Detail
        // string with the secret value already replaced. Reporting must carry the redacted
        // marker through unchanged — never attempt to "restore" or otherwise touch it.
        var config = EntityFactory.Configuration(@"D:\Redacted\web.config", dependencyReferences: ["FileShare: [REDACTED]"]);
        var entities = new List<DiscoveryEntity> { config };

        var report = TestPipeline.Run(entities);
        var json = new JsonReportRenderer().Render(report).Content;

        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Structural tripwire (§6): none of the JSON contract DTOs expose a raw entity-Metadata-
    /// shaped property. If a future change ever added one, this test fails immediately rather
    /// than relying solely on content-based negative tests to catch it.
    /// </summary>
    [Fact]
    public void NoDtoType_ExposesAMetadataOrRawSecretShapedProperty()
    {
        var dtoTypes = typeof(ServerReportDto).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ServerReportDto).Namespace)
            .ToList();

        Assert.NotEmpty(dtoTypes);

        var forbiddenNameFragments = new[] { "Metadata", "Password", "Secret", "ApiKey", "ConnectionString", "PrivateKey", "Token", "RawContent" };

        foreach (var type in dtoTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var fragment in forbiddenNameFragments)
                {
                    Assert.False(
                        property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                        $"{type.Name}.{property.Name} matches forbidden name fragment '{fragment}' — this would reopen the secret-safety boundary skill.md §6 requires.");
                }
            }
        }
    }
}
