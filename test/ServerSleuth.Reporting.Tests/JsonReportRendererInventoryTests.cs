using System.Text.Json;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// GUI-9A — proves the JSON report carries the same nine inventory sections the HTML report
/// already does (see <c>HtmlReportRendererInventoryFirstTests</c>): real discovered entities
/// (DLLs, services, runtimes, scheduled tasks, certificates, configuration) appear in the JSON
/// contract's inventory list fields when discovery/boundary data is supplied, using the real
/// <see cref="ReportDtoMapper"/>/<see cref="JsonReportRenderer"/> architecture — never a second
/// mapping path, never fabricated data.
/// </summary>
public class JsonReportRendererInventoryTests
{
    private static string BuildJson()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV\QINV_WEB_NOURM");
        var pool = EntityFactory.ApplicationPool("QINVAppPool");
        var app = EntityFactory.Application("QINV", "/TEST", @"C:\QINV\QINV_WEB_NOURM", poolId: pool.Id, siteId: site.Id);

        var dapper = EntityFactory.Dll(@"C:\QINV\QINV_WEB_NOURM\Bin\Dapper.dll", referencedBy: [app.Id]);
        var svc = EntityFactory.Service("QINVWorker", @"C:\QINV\Worker\QINVWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\QINV\Nightly", @"C:\QINV\Worker\QINVWorker.exe");
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.4");
        var cert = EntityFactory.Certificate("qinv.example.com", "ABC123", validTo: DateTimeOffset.UtcNow.AddYears(1));
        var config = EntityFactory.Configuration(@"C:\QINV\QINV_WEB_NOURM\web.config", ownerEntityId: app.Id);

        var entities = new List<DiscoveryEntity> { site, pool, app, dapper, svc, task, runtime, cert, config };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        return new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
    }

    [Fact]
    public void DllBinaries_ContainsRealDiscoveredEntity_WhenInventoryDataSupplied()
    {
        var json = BuildJson();
        using var doc = JsonDocument.Parse(json);

        var dllBinaries = doc.RootElement.GetProperty("DllBinaries");
        Assert.True(dllBinaries.GetArrayLength() > 0);
        Assert.Contains(
            dllBinaries.EnumerateArray(),
            e => e.GetProperty("Name").GetString() == "Dapper.dll");
    }

    [Fact]
    public void ScheduledTasks_ContainsRealDiscoveredEntity_WhenInventoryDataSupplied()
    {
        var json = BuildJson();
        using var doc = JsonDocument.Parse(json);

        var scheduledTasks = doc.RootElement.GetProperty("ScheduledTasks");
        Assert.True(scheduledTasks.GetArrayLength() > 0);
        Assert.Contains(
            scheduledTasks.EnumerateArray(),
            e => e.GetProperty("Name").GetString() == "Nightly");
    }

    [Fact]
    public void AllNineInventoryFields_ArePopulatedOrEmpty_NeverMissing()
    {
        var json = BuildJson();
        using var doc = JsonDocument.Parse(json);

        string[] fields =
        [
            "DllBinaries", "Runtimes", "Services", "ComComponents", "Software",
            "ScheduledTasks", "Certificates", "Configurations", "ExternalConnections"
        ];

        foreach (var field in fields)
        {
            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty(field).ValueKind);
        }
    }

    [Fact]
    public void ComComponentsAndSoftware_RemainEmpty_WhenNoSuchEntitiesDiscovered()
    {
        var json = BuildJson();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(0, doc.RootElement.GetProperty("ComComponents").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("Software").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("ExternalConnections").GetArrayLength());
    }

    [Fact]
    public void OmittingInventoryParameters_KeepsAllNineInventoryFieldsEmpty_BackwardCompatible()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV\QINV_WEB_NOURM");
        var entities = new List<DiscoveryEntity> { site };
        var report = TestPipeline.Run(entities);

        var json = new JsonReportRenderer().Render(report).Content;
        using var doc = JsonDocument.Parse(json);

        string[] fields =
        [
            "DllBinaries", "Runtimes", "Services", "ComComponents", "Software",
            "ScheduledTasks", "Certificates", "Configurations", "ExternalConnections"
        ];

        foreach (var field in fields)
        {
            Assert.Equal(0, doc.RootElement.GetProperty(field).GetArrayLength());
        }
    }

    [Fact]
    public void Configuration_NeverExposesRawContentOrSecretDetectedField()
    {
        var json = BuildJson();
        using var doc = JsonDocument.Parse(json);

        var configurations = doc.RootElement.GetProperty("Configurations");
        Assert.True(configurations.GetArrayLength() > 0);

        foreach (var config in configurations.EnumerateArray())
        {
            foreach (var property in config.EnumerateObject())
            {
                Assert.NotEqual("SecretDetected", property.Name);
                Assert.NotEqual("RawContent", property.Name);
                Assert.NotEqual("Content", property.Name);
            }
        }
    }
}
