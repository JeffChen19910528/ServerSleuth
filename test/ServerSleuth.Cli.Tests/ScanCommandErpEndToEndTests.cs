using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Runs the exact same 17-entity ERP fixture used by every prior phase's own fixture tests
/// through the real CLI composition layer (a fake <c>IDiscoveryEngine</c> substitutes for actual
/// scanning; every stage after that — Correlation through Export — is the real pipeline) — see
/// skill.md (Phase 10A) §24. Nothing is hard-coded in the CLI; every assertion reads the actual
/// exported files/console output.
/// </summary>
public class ScanCommandErpEndToEndTests
{
    [Fact]
    public async Task ErpFixture_ProducesBothReports_WithEstablishedSemantics()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Empty(stderr);

        var jsonPath = Path.Combine(temp.Path, "report.json");
        var htmlPath = Path.Combine(temp.Path, "report.html");
        Assert.True(File.Exists(jsonPath));
        Assert.True(File.Exists(htmlPath));

        var json = await File.ReadAllTextAsync(jsonPath);
        var html = await File.ReadAllTextAsync(htmlPath);

        // JSON still carries the full internal Risk/Migration assessment (unaffected by the HTML
        // report redesign — only the HTML renderer stopped surfacing it).
        Assert.Contains("\"OverallMigrationStatus\": \"Blocked\"", json, StringComparison.Ordinal);
        Assert.Contains("boundary:iis-application:ERP:/", json, StringComparison.Ordinal);
        Assert.Contains("boundary:service:ERPWorker", json, StringComparison.Ordinal);
        Assert.Contains("boundary:service:BatchA", json, StringComparison.Ordinal);
        Assert.Contains("boundary:service:BatchB", json, StringComparison.Ordinal);

        // HTML shows the deployed applications (Server Deployment Inventory), never a
        // Risk/Migration status badge.
        Assert.Contains(">ERP<", html, StringComparison.Ordinal);
        Assert.Contains(">ERPWorker<", html, StringComparison.Ordinal);
        Assert.Contains(">BatchA<", html, StringComparison.Ordinal);
        Assert.Contains(">BatchB<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("badge status-", html, StringComparison.Ordinal);

        // Console progress reflects the same numbers.
        Assert.Contains("Discovery complete", stdout, StringComparison.Ordinal);
        Assert.Contains("Migration:", stdout, StringComparison.Ordinal);
        Assert.Contains("Blocked:             1", stdout, StringComparison.Ordinal);
        Assert.Contains("ReadyWithConditions: 3", stdout, StringComparison.Ordinal);
        Assert.Contains("report.json", stdout, StringComparison.Ordinal);
        Assert.Contains("report.html", stdout, StringComparison.Ordinal);
        Assert.Contains("Completed.", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedHostExe_IsOneLogicalDependency_WithThreeAffectedBoundaries()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "json"], engine);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var shared = Assert.Single(doc.RootElement.GetProperty("SharedInfrastructure").EnumerateArray());
        Assert.Equal(3, shared.GetProperty("AffectedBoundaryIds").GetArrayLength());
    }

    [Theory]
    [InlineData("json", "report.json")]
    [InlineData("html", "report.html")]
    public async Task FormatOption_WritesOnlyTheRequestedFile(string format, string expectedFile)
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, _, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", format], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(temp.Path, expectedFile)));

        var other = expectedFile == "report.json" ? "report.html" : "report.json";
        Assert.False(File.Exists(Path.Combine(temp.Path, other)));
    }

    [Fact]
    public async Task DefaultOutputDirectory_IsUsedWhenNotSpecified()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        try
        {
            Directory.SetCurrentDirectory(temp.Path);
            var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

            var (exitCode, _, _) = await CliTestRunner.RunAsync(["scan"], engine);

            Assert.Equal(CliExitCode.Success, exitCode);
            Assert.True(File.Exists(Path.Combine(temp.Path, "serversleuth-report", "report.json")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}
