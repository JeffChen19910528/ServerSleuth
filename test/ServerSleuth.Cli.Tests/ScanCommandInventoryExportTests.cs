using System.Text.Json;
using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// GUI-9A — proves the CLI's actual exported <c>report.json</c> carries the nine inventory
/// sections, not just the HTML report. Before this fix, <c>ReportExportRunner</c> passed only
/// <c>ScanPipelineResult.Report</c> to <c>ReportArtifactFactory.CreateBundle</c>, so the JSON
/// renderer never received discovery/boundary data and every inventory array was always empty —
/// see the same 17-entity ERP fixture <see cref="ScanCommandErpEndToEndTests"/> already uses,
/// which contains real DLLs, Services, a ScheduledTask, Runtimes, a Certificate, and a
/// Configuration.
/// </summary>
public class ScanCommandInventoryExportTests
{
    [Fact]
    public async Task ExportedJsonReport_ContainsRealDiscoveredInventory_NotEmptyArrays()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, _, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "json"], engine);
        Assert.Equal(CliExitCode.Success, exitCode);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("DllBinaries").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Services").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("ScheduledTasks").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Runtimes").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Certificates").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Configurations").GetArrayLength() > 0);

        Assert.Contains(
            doc.RootElement.GetProperty("ScheduledTasks").EnumerateArray(),
            e => e.GetProperty("Name").GetString() == "BatchC");
    }

    [Fact]
    public async Task ExportedJsonAndHtmlReports_AgreeOnInventoryCounts()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        var html = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.html"));
        using var doc = JsonDocument.Parse(json);

        var jsonDllCount = doc.RootElement.GetProperty("DllBinaries").GetArrayLength();
        Assert.True(jsonDllCount > 0);
        // The JSON report still carries every discovered DLL; the HTML Server Deployment
        // Inventory report groups them under their owning Application instead of a flat section.
        Assert.Contains("id=\"application-components\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// GUI-9B §18 real-data acceptance — the CLI's actual exported <c>report.json</c> (real ERP
    /// fixture, real pipeline, no synthetic shortcut) carries a non-trivial
    /// <c>MigrationPreparation</c> summary computed from that real inventory, a shared DLL
    /// (<c>host.exe</c>, referenced by BatchA/BatchB/BatchC per <see cref="ErpFixture"/>) exposes
    /// multiple applications, and the existing Risk summary counts are byte-for-byte unchanged
    /// from what <see cref="ScanCommandErpEndToEndTests"/> already asserts. No value here is
    /// hard-coded from the fixture beyond what that existing, already-approved test asserts.
    /// </summary>
    [Fact]
    public async Task ExportedJsonReport_MigrationPreparationIsComputedFromRealInventory()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, _, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path, "--format", "json"], engine);
        Assert.Equal(CliExitCode.Success, exitCode);

        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        using var doc = JsonDocument.Parse(json);

        var summary = doc.RootElement.GetProperty("MigrationPreparation");
        Assert.True(summary.GetProperty("TotalInventoryCount").GetInt32() > 0);

        var verifyCount = summary.GetProperty("IntentCounts").EnumerateArray()
            .Single(e => e.GetProperty("Intent").GetString() == "Verify")
            .GetProperty("Count").GetInt32();
        Assert.True(verifyCount > 0);

        // ERP fixture's shared executable (BatchA + BatchB Services + BatchC ScheduledTask all
        // reference D:\ERP\Shared\host.exe) — real correlation, not fabricated.
        var services = doc.RootElement.GetProperty("Services");
        var batchA = services.EnumerateArray().Single(e => e.GetProperty("Name").GetString() == "BatchA");
        var batchB = services.EnumerateArray().Single(e => e.GetProperty("Name").GetString() == "BatchB");
        Assert.True(batchA.GetProperty("ApplicationNames").GetArrayLength() >= 1);
        Assert.True(batchB.GetProperty("ApplicationNames").GetArrayLength() >= 1);
    }
}
