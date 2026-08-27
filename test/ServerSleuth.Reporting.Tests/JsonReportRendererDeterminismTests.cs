using System.Text.RegularExpressions;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Deterministic rendering — see skill.md (Phase 9A) §8. The same
/// <c>ServerMigrationAssessmentReport</c> rendered twice (or ten times) must produce
/// byte-identical JSON, since every collection in the DTO tree is built from Phase 8C's own
/// already-ordinal-sorted lists and no dictionary/hash-set is ever enumerated along the way.</summary>
public class JsonReportRendererDeterminismTests
{
    private static List<DiscoveryEntity> BuildScenario()
    {
        var serviceA = EntityFactory.Service("DetA", @"D:\Det\host.exe");
        var serviceB = EntityFactory.Service("DetB", @"D:\Det\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Det\DetC", @"D:\Det\host.exe");
        var exe = EntityFactory.Dll(@"D:\Det\host.exe");
        var expiring = EntityFactory.Certificate("det.example.com", "DETCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));

        return [serviceA, serviceB, taskC, exe, expiring];
    }

    [Fact]
    public void RenderingTheSameReport_TwiceProducesByteIdenticalJson()
    {
        var report = TestPipeline.Run(BuildScenario());
        var renderer = new JsonReportRenderer();

        var first = renderer.Render(report);
        var second = renderer.Render(report);

        Assert.Equal(first.Content, second.Content, StringComparer.Ordinal);
    }

    [Fact]
    public void RenderingTheSameReport_TenTimesProducesIdenticalJson()
    {
        var report = TestPipeline.Run(BuildScenario());
        var renderer = new JsonReportRenderer();

        var results = Enumerable.Range(0, 10).Select(_ => renderer.Render(report).Content).ToList();

        Assert.All(results, r => Assert.Equal(results[0], r, StringComparer.Ordinal));
    }

    // §17: order must never depend on Dictionary/HashSet/registration-order artifacts. Building
    // the identical entity SET in reverse construction order must still yield the same JSON
    // (ignoring EvidenceRecord.CapturedAt, which is a genuine wall-clock construction timestamp —
    // not something the renderer controls or should normalize away in production output).
    [Fact]
    public void SameEntitySet_BuiltInReverseOrder_ProducesIdenticalJson_IgnoringCapturedAt()
    {
        var forward = BuildScenario();
        var reversed = Enumerable.Reverse(forward).ToList();

        var renderer = new JsonReportRenderer();
        var forwardJson = StripCapturedAt(renderer.Render(TestPipeline.Run(forward)).Content);
        var reversedJson = StripCapturedAt(renderer.Render(TestPipeline.Run(reversed)).Content);

        Assert.Equal(forwardJson, reversedJson, StringComparer.Ordinal);
    }

    private static string StripCapturedAt(string json) =>
        Regex.Replace(json, "\"CapturedAt\": \"[^\"]*\"", "\"CapturedAt\": \"REDACTED\"");
}
