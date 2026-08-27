using ServerSleuth.Analysis.Risk.Engine;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

/// <summary>
/// Determinism and no-mutation validation at moderate scale (skill.md Phase 7A §32-33). Full
/// 5,000-entity-scale coverage lives in <c>RiskPerformanceTests</c>; this file focuses on
/// asserting bit-for-bit-identical repeated results and zero input mutation, which are cheap to
/// check precisely at a few hundred entities.
/// </summary>
public class RiskDeterminismTests
{
    private static List<DiscoveryEntity> BuildModerateFixture()
    {
        var entities = new List<DiscoveryEntity>();

        for (var i = 0; i < 100; i++)
        {
            var missing = i % 5 == 0; // one in five workers has a missing executable
            var service = EntityFactory.Service($"Worker{i}", $@"D:\Workers\Worker{i}\worker{i}.exe");
            var exe = EntityFactory.Dll($@"D:\Workers\Worker{i}\worker{i}.exe", notFound: missing);
            entities.Add(service);
            entities.Add(exe);
        }

        for (var i = 0; i < 50; i++)
        {
            var config = EntityFactory.Configuration($@"D:\Configs\app{i}.config",
                dependencyReferences: i % 3 == 0 ? ["RuntimeVersion: net8.0"] : []);
            entities.Add(config);
        }

        entities.Add(EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.0")); // net8.0 requirement above is never satisfied

        for (var i = 0; i < 20; i++)
        {
            var validTo = i % 4 == 0 ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddYears(1);
            entities.Add(EntityFactory.Certificate($"host{i}.example.com", $"THUMB{i}", validTo: validTo));
        }

        return entities;
    }

    [Fact]
    public void RepeatedAnalyze_OnSameContext_ProducesIdenticalFindingsIdsOrderAndDiagnostics()
    {
        var (_, context) = RiskPipeline.Run(BuildModerateFixture());
        var engine = new RiskRuleEngine(RiskPipeline.AllRules);

        var resultA = engine.Analyze(context);
        var resultB = engine.Analyze(context);

        Assert.Equal(resultA.Findings.Count, resultB.Findings.Count);
        Assert.True(resultA.Findings.Count > 0); // fixture must actually produce findings for this to be meaningful
        Assert.Equal(resultA.Findings.Select(f => f.Id), resultB.Findings.Select(f => f.Id));
        Assert.Equal(resultA.Findings.Select(f => f.Severity), resultB.Findings.Select(f => f.Severity));
        Assert.Equal(resultA.Findings.Select(f => f.Confidence.Value), resultB.Findings.Select(f => f.Confidence.Value));
        Assert.Equal(resultA.Findings.SelectMany(f => f.Evidence.Select(e => (e.Type, e.Location, e.Detail))),
                     resultB.Findings.SelectMany(f => f.Evidence.Select(e => (e.Type, e.Location, e.Detail))));
        Assert.Equal(resultA.Diagnostics.RulesEvaluated, resultB.Diagnostics.RulesEvaluated);
        Assert.Equal(resultA.Diagnostics.FindingsCreated, resultB.Diagnostics.FindingsCreated);
        Assert.Equal(resultA.Diagnostics.FindingsDeduplicated, resultB.Diagnostics.FindingsDeduplicated);
        Assert.Equal(resultA.Diagnostics.RuleFailures.Count, resultB.Diagnostics.RuleFailures.Count);
    }

    [Fact]
    public void Analyze_NeverMutatesInputEntitiesGraphBoundariesExpansionOrValidation()
    {
        var entities = BuildModerateFixture();
        var (_, context) = RiskPipeline.Run(entities);

        var entityCountBefore = context.AllEntities.Count;
        var entityIdsBefore = context.AllEntities.Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var nodeCountBefore = context.Graph.Nodes.Count;
        var edgeCountBefore = context.Graph.Edges.Count;
        var edgeShapeBefore = context.Graph.Edges.Select(e => (e.SourceEntityId, e.TargetEntityId, e.Type)).OrderBy(x => x.SourceEntityId, StringComparer.Ordinal).ToList();
        var boundaryCountBefore = context.Boundaries.Count;
        var boundaryIdsBefore = context.Boundaries.Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var externalDependencyCountBefore = context.Expansion.ExternalDependencies.Count;
        var validationFindingCountBefore = context.Validation.Findings.Count;

        var engine = new RiskRuleEngine(RiskPipeline.AllRules);
        engine.Analyze(context);
        engine.Analyze(context); // run twice for good measure — mutation on the second pass would still be caught below

        Assert.Equal(entityCountBefore, context.AllEntities.Count);
        Assert.Equal(entityIdsBefore, context.AllEntities.Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal).ToList());
        Assert.Equal(nodeCountBefore, context.Graph.Nodes.Count);
        Assert.Equal(edgeCountBefore, context.Graph.Edges.Count);
        Assert.Equal(edgeShapeBefore, context.Graph.Edges.Select(e => (e.SourceEntityId, e.TargetEntityId, e.Type)).OrderBy(x => x.SourceEntityId, StringComparer.Ordinal).ToList());
        Assert.Equal(boundaryCountBefore, context.Boundaries.Count);
        Assert.Equal(boundaryIdsBefore, context.Boundaries.Select(b => b.Id).OrderBy(id => id, StringComparer.Ordinal).ToList());
        Assert.Equal(externalDependencyCountBefore, context.Expansion.ExternalDependencies.Count);
        Assert.Equal(validationFindingCountBefore, context.Validation.Findings.Count);
    }
}
