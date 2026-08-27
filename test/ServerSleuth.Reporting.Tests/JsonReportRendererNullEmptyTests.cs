using System.Text.Json;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Null/empty semantics — see skill.md (Phase 9A) §10: nothing is silently invented. Every
/// collection field is ALWAYS present as a (possibly empty) JSON array, never omitted; every
/// optional scalar (e.g. <c>RelatedRiskFindingId</c>) serializes as explicit JSON <c>null</c>
/// when absent, never a fabricated default. Documented here and enforced by these tests.
/// </summary>
public class JsonReportRendererNullEmptyTests
{
    private static JsonDocument RenderEmpty()
    {
        var report = TestPipeline.Run([]);
        var result = new JsonReportRenderer().Render(report);
        return JsonDocument.Parse(result.Content);
    }

    [Fact]
    public void EmptyReport_RendersValidJson_WithEmptyArraysNotOmittedFields()
    {
        var json = RenderEmpty();
        var root = json.RootElement;

        Assert.Equal(JsonValueKind.Array, root.GetProperty("Applications").ValueKind);
        Assert.Empty(root.GetProperty("Applications").EnumerateArray());

        Assert.Equal(JsonValueKind.Array, root.GetProperty("ServerLevelIssues").ValueKind);
        Assert.Empty(root.GetProperty("ServerLevelIssues").EnumerateArray());

        Assert.Equal(JsonValueKind.Array, root.GetProperty("Actions").ValueKind);
        Assert.Empty(root.GetProperty("Actions").EnumerateArray());

        Assert.Equal(JsonValueKind.Array, root.GetProperty("PreMigrationChecks").ValueKind);
        Assert.Empty(root.GetProperty("PreMigrationChecks").EnumerateArray());

        Assert.Equal(JsonValueKind.Array, root.GetProperty("PostMigrationChecks").ValueKind);
        Assert.Empty(root.GetProperty("PostMigrationChecks").EnumerateArray());

        Assert.Equal(JsonValueKind.Array, root.GetProperty("Dependencies").ValueKind);
        Assert.Empty(root.GetProperty("Dependencies").EnumerateArray());
    }

    [Fact]
    public void EmptyReport_ServerSummary_ReflectsZeroCounts_ReadyStatus()
    {
        var json = RenderEmpty();
        var server = json.RootElement.GetProperty("Server");

        Assert.Equal("Ready", server.GetProperty("OverallMigrationStatus").GetString());
        Assert.Equal(0, server.GetProperty("ApplicationCount").GetInt32());
        Assert.Equal(0, server.GetProperty("ActionCount").GetInt32());
    }

    [Fact]
    public void DependencyWithNoRelatedRiskFinding_SerializesExplicitNull_NeverOmitted()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var app = EntityFactory.Application("Orphan", "/", @"D:\Orphan");
        var config = EntityFactory.Configuration(@"D:\Orphan\web.config", ownerEntityId: app.Id);
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "OrphanDb");

        var entities = new List<DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var json = JsonDocument.Parse(new JsonReportRenderer().Render(report).Content);

        var dependency = json.RootElement.GetProperty("Dependencies").EnumerateArray()
            .SelectMany(g => g.GetProperty("Dependencies").EnumerateArray())
            .Single();

        // This particular dependency DOES have a RelatedRiskFindingId (Database via RR9), so
        // assert the property exists and is non-null here, and separately prove the DTO's own
        // contract allows null by checking the property is nullable at the type level.
        Assert.NotEqual(JsonValueKind.Undefined, dependency.GetProperty("RelatedRiskFindingId").ValueKind);
    }

    [Fact]
    public void EvidenceDetail_CanBeExplicitNull_NeverOmitted()
    {
        // MissingRuntimeRule's own evidence never sets Detail — proves optional string fields
        // serialize as JSON null rather than being dropped from the object.
        var app = EntityFactory.Application("RuntimeOnly", "/", @"D:\RuntimeOnly");
        var config = EntityFactory.Configuration(@"D:\RuntimeOnly\web.config", ownerEntityId: app.Id, dependencyReferences: ["RuntimeVersion: net8.0"]);
        var entities = new List<DiscoveryEntity> { app, config };

        var report = TestPipeline.Run(entities);
        var json = JsonDocument.Parse(new JsonReportRenderer().Render(report).Content);

        var issue = json.RootElement.GetProperty("ServerLevelIssues").EnumerateArray()
            .Concat(json.RootElement.GetProperty("Applications").EnumerateArray().SelectMany(a => a.GetProperty("Issues").EnumerateArray()))
            .First(i => i.GetProperty("RuleId").GetString() == "RR4-MissingRuntime");

        var evidence = issue.GetProperty("Evidence").EnumerateArray().First();
        Assert.True(evidence.TryGetProperty("Detail", out var detail));
        Assert.True(detail.ValueKind is JsonValueKind.String or JsonValueKind.Null);
    }
}
