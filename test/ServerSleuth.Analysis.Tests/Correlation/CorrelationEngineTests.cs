using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation;

public class CorrelationEngineTests
{
    private sealed class FixedCandidateRule(CorrelationCandidate candidate) : ICorrelationRule
    {
        public string Id => "fixed-rule";
        public IReadOnlyList<CorrelationCandidate> Evaluate(CorrelationContext context) => [candidate];
    }

    private static Application App(string id) => new()
    {
        Id = id, Name = id, Type = "Application", Source = "Test", Confidence = Confidence.VeryHigh()
    };

    [Fact]
    public void Correlate_CandidateWithNoEvidence_IsRejected_NoEdgeCreated()
    {
        var a = App("a");
        var b = App("b");
        var candidate = new CorrelationCandidate
        {
            RuleId = "r", SourceEntityId = a.Id, TargetEntityId = b.Id,
            Type = DependencyEdgeType.Uses, Confidence = Confidence.High(), Evidence = []
        };

        var engine = new CorrelationEngine([new FixedCandidateRule(candidate)]);
        var result = engine.Correlate([a, b]);

        Assert.Empty(result.Graph.Edges);
        Assert.Single(result.Diagnostics.Rejected);
        Assert.Contains("evidence", result.Diagnostics.Rejected[0].Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Correlate_SelfEdgeCandidate_IsRejected()
    {
        var a = App("a");
        var candidate = new CorrelationCandidate
        {
            RuleId = "r", SourceEntityId = a.Id, TargetEntityId = a.Id,
            Type = DependencyEdgeType.Uses, Confidence = Confidence.High(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "x" }]
        };

        var engine = new CorrelationEngine([new FixedCandidateRule(candidate)]);
        var result = engine.Correlate([a]);

        Assert.Empty(result.Graph.Edges);
        Assert.Contains(result.Diagnostics.Rejected, r => r.Reason.Contains("Self-edge"));
    }

    [Fact]
    public void Correlate_TargetNotInDiscoveryInput_IsRejected()
    {
        var a = App("a");
        var candidate = new CorrelationCandidate
        {
            RuleId = "r", SourceEntityId = a.Id, TargetEntityId = "does-not-exist",
            Type = DependencyEdgeType.Uses, Confidence = Confidence.High(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "x" }]
        };

        var engine = new CorrelationEngine([new FixedCandidateRule(candidate)]);
        var result = engine.Correlate([a]);

        Assert.Empty(result.Graph.Edges);
        Assert.Contains(result.Diagnostics.Rejected, r => r.Reason.Contains("Target entity"));
    }

    [Fact]
    public void Correlate_UnresolvedTargetCandidate_IsRejectedNotGuessed()
    {
        var a = App("a");
        var candidate = new CorrelationCandidate
        {
            RuleId = "r", SourceEntityId = a.Id, TargetEntityId = null,
            Type = DependencyEdgeType.Uses, Confidence = Confidence.High(),
            UnresolvedReason = "could not resolve"
        };

        var engine = new CorrelationEngine([new FixedCandidateRule(candidate)]);
        var result = engine.Correlate([a]);

        Assert.Empty(result.Graph.Edges);
        Assert.Single(result.Diagnostics.Rejected);
        Assert.Equal("could not resolve", result.Diagnostics.Rejected[0].Reason);
    }

    [Fact]
    public void Correlate_TwoRulesProduceSameLogicalEdge_MergesIntoOneEdgeWithBothEvidence()
    {
        var a = App("a");
        var b = App("b");
        var evidence1 = new EvidenceRecord { Type = EvidenceType.Registry, Location = "source-1" };
        var evidence2 = new EvidenceRecord { Type = EvidenceType.FileSystem, Location = "source-2" };

        var candidate1 = new CorrelationCandidate
        {
            RuleId = "r1", SourceEntityId = a.Id, TargetEntityId = b.Id,
            Type = DependencyEdgeType.Uses, Confidence = Confidence.Medium(), Evidence = [evidence1]
        };
        var candidate2 = new CorrelationCandidate
        {
            RuleId = "r2", SourceEntityId = a.Id, TargetEntityId = b.Id,
            Type = DependencyEdgeType.Uses, Confidence = Confidence.VeryHigh(), Evidence = [evidence2]
        };

        var engine = new CorrelationEngine([new FixedCandidateRule(candidate1), new FixedCandidateRule(candidate2)]);
        var result = engine.Correlate([a, b]);

        var edge = Assert.Single(result.Graph.Edges);
        Assert.Equal(2, edge.Evidence.Count);
        Assert.Equal(ConfidenceBand.VeryHigh, edge.Confidence.Band); // higher of the two wins
        Assert.Equal(1, result.Diagnostics.EdgesCreated);
        Assert.Equal(1, result.Diagnostics.DuplicatesMerged);
    }

    [Fact]
    public void Correlate_GivenIdenticalInputTwice_ProducesDeterministicGraph()
    {
        var app = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var site = EntityFactory.Site("ERP");
        var pool = EntityFactory.ApplicationPool("ERPAppPool");
        app = EntityFactory.Application("ERP", "/", @"D:\ERP", poolId: pool.Id);

        var entities = new List<DiscoveryEntity> { site, pool, app };

        var engine = new CorrelationEngine();
        var result1 = engine.Correlate(entities);
        var result2 = engine.Correlate(entities);

        Assert.Equal(result1.Graph.Edges.Count, result2.Graph.Edges.Count);
        Assert.Equal(
            result1.Graph.Edges.Select(e => (e.SourceEntityId, e.TargetEntityId, e.Type)).OrderBy(t => t),
            result2.Graph.Edges.Select(e => (e.SourceEntityId, e.TargetEntityId, e.Type)).OrderBy(t => t));
    }

    [Fact]
    public void Correlate_DuplicateEntityInInput_AddedAsSingleNode()
    {
        var a = App("a");
        var engine = new CorrelationEngine([]);
        var result = engine.Correlate([a, a]);

        Assert.Single(result.Graph.Nodes);
    }
}
