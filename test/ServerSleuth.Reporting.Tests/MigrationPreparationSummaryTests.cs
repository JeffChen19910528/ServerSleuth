using System.Text.Json;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// GUI-9B §7, §8, §11 — proves <c>ServerReportDto.MigrationPreparation</c> is a deterministic,
/// inventory-derived projection: correct counts, no Risk dependency, no execution, distinct
/// "Inventory Count" vs. "Migration Intent Count" semantics.
/// </summary>
public class MigrationPreparationSummaryTests
{
    private static JsonElement BuildSummary(List<DiscoveryEntity> entities)
    {
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var json = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("MigrationPreparation").Clone();
    }

    private static int IntentCount(JsonElement summary, string intent) =>
        summary.GetProperty("IntentCounts").EnumerateArray()
            .Single(e => e.GetProperty("Intent").GetString() == intent)
            .GetProperty("Count").GetInt32();

    [Fact]
    public void EmptyDiscovery_AllIntentCountsAreZero_AndTotalIsZero()
    {
        var summary = BuildSummary([]);

        Assert.Equal(0, summary.GetProperty("TotalInventoryCount").GetInt32());
        foreach (var entry in summary.GetProperty("IntentCounts").EnumerateArray())
        {
            Assert.Equal(0, entry.GetProperty("Count").GetInt32());
        }
    }

    [Fact]
    public void AllSevenIntents_AlwaysPresent_EvenWhenCountIsZero()
    {
        var summary = BuildSummary([]);
        var intents = summary.GetProperty("IntentCounts").EnumerateArray()
            .Select(e => e.GetProperty("Intent").GetString()).ToList();

        Assert.Equal(["Deploy", "Install", "Create", "Register", "Configure", "Verify", "Review"], intents);
    }

    [Fact]
    public void OneServiceEntity_ContributesToCreateConfigureAndVerify_NotAsThreeEntities()
    {
        // A lone Service anchor forms its own ApplicationBoundary (a real, expected pipeline
        // outcome), so this fixture yields two inventory entries: the Service itself, and the
        // "Application" category entry for its own boundary — both map to the same three
        // intents here, which is why Create/Configure/Verify each read 2, not 1.
        var service = EntityFactory.Service("Worker", @"C:\Worker\worker.exe");
        var summary = BuildSummary([service]);

        Assert.Equal(2, summary.GetProperty("TotalInventoryCount").GetInt32());

        // Migration Intent Count: each contributing entity adds +1 to every intent its category
        // maps to — this is intentional (skill.md GUI-9B §8), never mistaken for a unique-entity
        // count.
        Assert.Equal(2, IntentCount(summary, "Create"));
        Assert.Equal(2, IntentCount(summary, "Configure"));
        Assert.Equal(2, IntentCount(summary, "Verify"));
        Assert.Equal(0, IntentCount(summary, "Deploy"));
        Assert.Equal(0, IntentCount(summary, "Register"));
        Assert.Equal(0, IntentCount(summary, "Install"));
    }

    [Fact]
    public void MultipleCategories_VerifyCountIsTheSumAcrossAllOfThem()
    {
        // Dll (Deploy, Verify), Runtime (Install, Verify), Service (Create, Configure, Verify),
        // plus the Service's own ApplicationBoundary (Create, Configure, Verify) — all four
        // contributing entries include Verify, so Verify sums to 4.
        var dll = EntityFactory.Dll(@"C:\App\app.dll");
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.0");
        var service = EntityFactory.Service("Worker", @"C:\Worker\worker.exe");

        var summary = BuildSummary([dll, runtime, service]);

        Assert.Equal(4, summary.GetProperty("TotalInventoryCount").GetInt32());
        Assert.Equal(4, IntentCount(summary, "Verify"));
        Assert.Equal(1, IntentCount(summary, "Deploy"));
        Assert.Equal(1, IntentCount(summary, "Install"));
        Assert.Equal(2, IntentCount(summary, "Create"));
        Assert.Equal(2, IntentCount(summary, "Configure"));
    }

    [Fact]
    public void ReviewIntent_IsZero_WhenNoCategoryMapsToIt()
    {
        var dll = EntityFactory.Dll(@"C:\App\app.dll");
        var service = EntityFactory.Service("Worker", @"C:\Worker\worker.exe");
        var summary = BuildSummary([dll, service]);

        Assert.Equal(0, IntentCount(summary, "Review"));
    }

    [Fact]
    public void Summary_IsDeterministic_AcrossRepeatedRenders()
    {
        var dll = EntityFactory.Dll(@"C:\App\app.dll");
        var service = EntityFactory.Service("Worker", @"C:\Worker\worker.exe");
        var entities = new List<DiscoveryEntity> { dll, service };

        var first = BuildSummary(entities).GetRawText();
        var second = BuildSummary(entities).GetRawText();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// GUI-9B §7, §11 — the summary must not change when the underlying Risk assessment differs
    /// but the inventory does not: a missing-dependency Dll (which produces a Risk finding/Action)
    /// contributes exactly the same Deploy/Verify counts as a healthy one with zero findings.
    /// Proves the summary is inventory-derived, not Risk-derived, using an actual pipeline run
    /// rather than only a source-code scan.
    /// </summary>
    [Fact]
    public void Summary_IsUnaffectedByWhetherTheEntityHasARiskFinding()
    {
        var healthyDll = EntityFactory.Dll(@"C:\App\healthy.dll");
        var missingDll = EntityFactory.Dll(@"C:\App\missing.dll", notFound: true);

        var healthySummary = BuildSummary([healthyDll]);
        var missingSummary = BuildSummary([missingDll]);

        Assert.Equal(healthySummary.GetRawText(), missingSummary.GetRawText());
    }

    [Fact]
    public void OmittingInventoryParameters_KeepsMigrationPreparationEmpty_BackwardCompatible()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV");
        var report = TestPipeline.Run(new List<DiscoveryEntity> { site });

        var json = new JsonReportRenderer().Render(report).Content;
        using var doc = JsonDocument.Parse(json);
        var summary = doc.RootElement.GetProperty("MigrationPreparation");

        Assert.Equal(0, summary.GetProperty("TotalInventoryCount").GetInt32());
        foreach (var entry in summary.GetProperty("IntentCounts").EnumerateArray())
        {
            Assert.Equal(0, entry.GetProperty("Count").GetInt32());
        }
    }
}
