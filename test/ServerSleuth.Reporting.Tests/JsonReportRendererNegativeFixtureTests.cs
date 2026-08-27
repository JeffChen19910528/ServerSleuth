using System.Text.Json;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Negative fixtures for the JSON renderer — see skill.md (Phase 9A) §13, §23. Nothing
/// should disappear or fail to render across any of these edge cases.</summary>
public class JsonReportRendererNegativeFixtureTests
{
    private static JsonDocument Render(List<DiscoveryEntity> entities)
    {
        var report = TestPipeline.Run(entities);
        var result = new JsonReportRenderer().Render(report);
        return JsonDocument.Parse(result.Content);
    }

    [Fact]
    public void EmptyDiscovery_RendersValidReadyJson()
    {
        var json = Render([]);
        Assert.Equal("Ready", json.RootElement.GetProperty("Server").GetProperty("OverallMigrationStatus").GetString());
        Assert.Empty(json.RootElement.GetProperty("Applications").EnumerateArray());
    }

    [Fact]
    public void NoFindings_RendersReady()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var json = Render([runtime]);
        Assert.Equal("Ready", json.RootElement.GetProperty("Server").GetProperty("OverallMigrationStatus").GetString());
    }

    [Fact]
    public void InfoOnlyFindings_RenderReady_WithServerLevelIssueVisible()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: ["EnvVar: APP_HOME"]);
        var json = Render([config]);

        Assert.Equal("Ready", json.RootElement.GetProperty("Server").GetProperty("OverallMigrationStatus").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("ServerLevelIssues").EnumerateArray());
    }

    [Fact]
    public void CriticalMissingBinary_RendersBlocked()
    {
        var service = EntityFactory.Service("NegSvc", @"D:\Neg\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg\svc.exe", notFound: true);
        var json = Render([service, missingExe]);

        Assert.Equal("Blocked", json.RootElement.GetProperty("Server").GetProperty("OverallMigrationStatus").GetString());
    }

    [Fact]
    public void SharedBinaryAcrossThreeBoundaries_RendersOneLogicalDependency()
    {
        var serviceA = EntityFactory.Service("NegA", @"D:\Neg\host.exe");
        var serviceB = EntityFactory.Service("NegB", @"D:\Neg\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Neg\NegC", @"D:\Neg\host.exe");
        var exe = EntityFactory.Dll(@"D:\Neg\host.exe");

        var json = Render([serviceA, serviceB, taskC, exe]);
        var shared = Assert.Single(json.RootElement.GetProperty("SharedInfrastructure").EnumerateArray());
        Assert.Equal(3, shared.GetProperty("AffectedBoundaryIds").GetArrayLength());
    }

    [Fact]
    public void ServerOnlyIssue_NoApplicationBoundary_StillRendersVisibly()
    {
        var expiring = EntityFactory.Certificate("negonly.example.com", "NEGONLY", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var json = Render([expiring]);

        Assert.Empty(json.RootElement.GetProperty("Applications").EnumerateArray());
        Assert.NotEmpty(json.RootElement.GetProperty("ServerLevelIssues").EnumerateArray());
        Assert.NotEmpty(json.RootElement.GetProperty("Actions").EnumerateArray());
    }

    [Fact]
    public void ExpectedOrphanRuntimeAndCertificate_RenderWithNoFalseFindings()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var certificate = EntityFactory.Certificate("orphan.example.com", "ORPHANCERT", validTo: DateTimeOffset.UtcNow.AddYears(2));

        var json = Render([runtime, certificate]);

        Assert.Empty(json.RootElement.GetProperty("Actions").EnumerateArray());
        Assert.Empty(json.RootElement.GetProperty("ServerLevelIssues").EnumerateArray());
    }
}
