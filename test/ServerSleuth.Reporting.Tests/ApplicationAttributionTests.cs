using System.Text.Json;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// GUI-9B §5, §10 — proves <c>InventoryEntityDto.ApplicationName</c>/<c>ApplicationNames</c>
/// correctly reflect <see cref="ServerSleuth.Core.Boundaries.ApplicationBoundary.MemberEntityIds"/>
/// membership: zero, one, two, and three boundaries. Ownership is never inferred from entity
/// names — every fixture here relies only on real boundary/correlation membership produced by
/// the actual pipeline (via <see cref="ScheduledTaskRunsBinaryRule"/>-style RUNS correlation
/// through a shared executable, exactly as production discovery would establish it).
/// </summary>
public class ApplicationAttributionTests
{
    private static JsonElement FindByName(JsonElement array, string name) =>
        array.EnumerateArray().Single(e => e.GetProperty("Name").GetString() == name);

    [Fact]
    public void EntityInNoBoundary_HasNullApplicationName_AndEmptyApplicationNames()
    {
        // A DLL nothing references and nothing runs never joins any ApplicationBoundary.
        var orphanDll = EntityFactory.Dll(@"C:\Standalone\orphan.dll");
        var entities = new List<DiscoveryEntity> { orphanDll };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var json = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
        using var doc = JsonDocument.Parse(json);

        var dll = FindByName(doc.RootElement.GetProperty("DllBinaries"), "orphan.dll");
        Assert.Equal(JsonValueKind.Null, dll.GetProperty("ApplicationName").ValueKind);
        Assert.Equal(0, dll.GetProperty("ApplicationNames").GetArrayLength());
    }

    [Fact]
    public void EntityInOneBoundary_HasSingleApplicationName_AndMatchingApplicationNames()
    {
        var site = EntityFactory.Site("ERP", @"C:\ERP\Web");
        var pool = EntityFactory.ApplicationPool("ERPPool");
        var app = EntityFactory.Application("ERP", "/", @"C:\ERP\Web", poolId: pool.Id, siteId: site.Id);
        var dll = EntityFactory.Dll(@"C:\ERP\Web\Bin\ERP.Core.dll", referencedBy: [app.Id]);

        var entities = new List<DiscoveryEntity> { site, pool, app, dll };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var json = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
        using var doc = JsonDocument.Parse(json);

        var dllDto = FindByName(doc.RootElement.GetProperty("DllBinaries"), "ERP.Core.dll");
        var appName = dllDto.GetProperty("ApplicationName").GetString();
        Assert.NotNull(appName);

        var names = dllDto.GetProperty("ApplicationNames").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal([appName], names);
    }

    private static (Application App, WebSite Site, ApplicationPool Pool) BuildIisApp(string siteName)
    {
        var site = EntityFactory.Site(siteName, $@"C:\{siteName}");
        var pool = EntityFactory.ApplicationPool($"{siteName}Pool");
        var app = EntityFactory.Application(siteName, "/", $@"C:\{siteName}", poolId: pool.Id, siteId: site.Id);
        return (app, site, pool);
    }

    [Fact]
    public void EntitySharedByTwoBoundaries_ApplicationNamesContainsBoth_ApplicationNameStaysBackwardCompatible()
    {
        // A shared DLL explicitly referenced by two different IIS Applications — real
        // ApplicationContainsBinaryRule correlation (ReferencedByEntityIds), not name-inferred.
        var (appErp, siteErp, poolErp) = BuildIisApp("ERP");
        var (appHr, siteHr, poolHr) = BuildIisApp("HR");
        var sharedDll = EntityFactory.Dll(@"C:\Shared\Common.Utils.dll", referencedBy: [appErp.Id, appHr.Id]);

        var entities = new List<DiscoveryEntity>
        {
            siteErp, poolErp, appErp, siteHr, poolHr, appHr, sharedDll
        };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var json = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
        using var doc = JsonDocument.Parse(json);

        // The shared entity still appears exactly once in the server-level inventory list.
        var dllArray = doc.RootElement.GetProperty("DllBinaries");
        Assert.Single(dllArray.EnumerateArray(), e => e.GetProperty("Name").GetString() == "Common.Utils.dll");

        var sharedDto = FindByName(dllArray, "Common.Utils.dll");
        var names = sharedDto.GetProperty("ApplicationNames").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("ERP", names);
        Assert.Contains("HR", names);

        // ApplicationName (singular) is still present and is one of the two — backward compatible,
        // never null just because the entity happens to be shared.
        var singularName = sharedDto.GetProperty("ApplicationName").GetString();
        Assert.Contains(singularName, names);
    }

    [Fact]
    public void EntitySharedByThreeBoundaries_ApplicationNamesContainsAllThree()
    {
        var (appA, siteA, poolA) = BuildIisApp("Zeta");
        var (appB, siteB, poolB) = BuildIisApp("Alpha");
        var (appC, siteC, poolC) = BuildIisApp("Mid");
        var sharedDll = EntityFactory.Dll(@"C:\Shared\Multi.Utils.dll", referencedBy: [appA.Id, appB.Id, appC.Id]);

        var entities = new List<DiscoveryEntity>
        {
            siteA, poolA, appA, siteB, poolB, appB, siteC, poolC, appC, sharedDll
        };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var json = new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
        using var doc = JsonDocument.Parse(json);

        var dto = FindByName(doc.RootElement.GetProperty("DllBinaries"), "Multi.Utils.dll");
        var names = dto.GetProperty("ApplicationNames").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Equal(3, names.Count);
        Assert.Contains("Zeta", names);
        Assert.Contains("Alpha", names);
        Assert.Contains("Mid", names);

        // Deterministic: sorted OrdinalIgnoreCase regardless of the order boundaries/entities
        // were constructed/discovered in.
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
    }
}
