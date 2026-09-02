using System.Text.Json;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// GUI-9B §9 — proves every supported inventory category survives Discovery →
/// ScanPipelineResult → ReportDtoMapper → ServerReportDto with no fabrication and no silent
/// disappearance: source count equals report count for each category independently, and zero
/// source items produce zero report items. Uses only synthetic, parameterized fixtures — no
/// hard-coded ERP/QINV server values (skill.md GUI-9B §9).
/// </summary>
public class InventoryCompletenessTests
{
    private static string RenderJson(List<DiscoveryEntity> entities, IReadOnlyList<ExternalDependency>? externalDeps = null)
    {
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        return new JsonReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: externalDeps ?? [])
            .Render(report).Content;
    }

    private static int ArrayLength(string json, string field)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(field).GetArrayLength();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void DllBinaries_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)EntityFactory.Dll($@"C:\App{i}\bin{i}.dll"))
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "DllBinaries"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Runtimes_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)EntityFactory.Runtime("DotNetRuntime", $"Runtime{i}", $"{i}.0.0"))
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "Runtimes"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Services_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)EntityFactory.Service($"Service{i}", $@"C:\Svc{i}\svc{i}.exe"))
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "Services"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ComComponents_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)new ComComponent
            {
                Id = $"com:{{{i}}}",
                Name = $"Component{i}",
                Type = "ComComponent",
                Source = "WindowsRegistry",
                Status = EntityStatus.Installed,
                Confidence = Confidence.VeryHigh(),
                Clsid = $"{{00000000-0000-0000-0000-{i:D12}}}"
            })
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "ComComponents"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Software_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)new Software
            {
                Id = $"software:App{i}",
                Name = $"App{i}",
                Type = "Software",
                Source = "WindowsRegistry",
                Status = EntityStatus.Installed,
                Confidence = Confidence.VeryHigh()
            })
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "Software"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ScheduledTasks_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)EntityFactory.ScheduledTask($@"\Tasks\Task{i}", $@"C:\Task{i}\task{i}.exe"))
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "ScheduledTasks"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Certificates_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)EntityFactory.Certificate($"host{i}.example.com", $"THUMB{i}"))
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "Certificates"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Configurations_SourceCountEqualsReportCount(int count)
    {
        var entities = Enumerable.Range(0, count)
            .Select(i => (DiscoveryEntity)EntityFactory.Configuration($@"C:\Config{i}\app{i}.config"))
            .ToList();

        var json = RenderJson(entities);
        Assert.Equal(count, ArrayLength(json, "Configurations"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void ExternalConnections_SourceCountEqualsReportCount(int count)
    {
        var externalDeps = Enumerable.Range(0, count)
            .Select(i => new ExternalDependency
            {
                Id = $"external:dep{i}",
                Name = $"Dependency{i}",
                Type = "ExternalDependency",
                Source = "ConfigurationAnalysis",
                Status = EntityStatus.Unknown,
                Confidence = Confidence.Medium(),
                Kind = "Database",
                Endpoint = $"dep{i}.example.com"
            })
            .ToList();

        var json = RenderJson([], externalDeps);
        Assert.Equal(count, ArrayLength(json, "ExternalConnections"));
    }

    [Fact]
    public void AllCategories_ZeroSourceItems_ProducesZeroReportItems_Simultaneously()
    {
        var json = RenderJson([]);

        string[] fields =
        [
            "DllBinaries", "Runtimes", "Services", "ComComponents", "Software",
            "ScheduledTasks", "Certificates", "Configurations", "ExternalConnections"
        ];

        foreach (var field in fields)
        {
            Assert.Equal(0, ArrayLength(json, field));
        }
    }
}
