using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Cli.Tests;

/// <summary>Security regression — see skill.md (Phase 10A) §25: neither console output (stdout/
/// stderr) nor the exported files may contain a deliberately-injected sensitive value.</summary>
public class ScanCommandSecretSafetyTests
{
    [Theory]
    [InlineData("Password=CliDeliberatelySensitive1")]
    [InlineData("API_KEY=CliDeliberatelySensitive2")]
    [InlineData("Bearer CliDeliberatelySensitive3")]
    [InlineData("DB_PASSWORD=CliDeliberatelySensitive4")]
    [InlineData("-----BEGIN PRIVATE KEY-----CliDeliberatelySensitive5-----END PRIVATE KEY-----")]
    public async Task SensitiveValue_NeverAppearsInConsoleOutput_OrExportedFiles(string sensitiveValue)
    {
        using var temp = new TempDirectory();

        var app = EntityFactory.Application("SecretCliApp", "/", @"D:\SecretCliApp");
        var config = EntityFactory.Configuration(@"D:\SecretCliApp\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "SecretDb");
        config.SetMetadata("Sensitive", sensitiveValue);

        var entities = new List<DiscoveryEntity> { app, config };
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(entities));

        // Phase 10B §13: --verbose surfaces MORE console detail (per-scanner status/durations) —
        // the secret-safety guarantee must hold there too, not only in the default progress view.
        var (_, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--verbose"], engine);

        Assert.DoesNotContain(sensitiveValue, stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, stderr, StringComparison.Ordinal);

        var json = await System.IO.File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        var html = await System.IO.File.ReadAllTextAsync(Path.Combine(temp.Path, "report.html"));
        Assert.DoesNotContain(sensitiveValue, json, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RawXmlConfigurationMarkup_NeverAppearsInConsoleOutput()
    {
        using var temp = new TempDirectory();
        var app = EntityFactory.Application("XmlCliApp", "/", @"D:\XmlCliApp");
        var config = EntityFactory.Configuration(@"D:\XmlCliApp\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["FileShare: \\\\FILESERVER\\Share"]);

        var entities = new List<DiscoveryEntity> { app, config };
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(entities));

        var (_, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.DoesNotContain("<configuration>", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionStrings", stdout, StringComparison.Ordinal);
    }
}
