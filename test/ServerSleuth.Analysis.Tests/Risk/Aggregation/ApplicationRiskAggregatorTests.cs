using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>See skill.md (Phase 7B) §4.</summary>
public class ApplicationRiskAggregatorTests
{
    private static ApplicationBoundary MakeBoundary(string id, string name) => new()
    {
        Id = id,
        Name = name,
        MemberEntityIds = [],
        Evidence = [],
        Confidence = Confidence.High(),
        Reason = "test boundary"
    };

    private static RiskFinding MakeFinding(string ruleId, string sourceId, RiskSeverity severity = RiskSeverity.Medium) => new()
    {
        Id = RiskFinding.ComputeId(ruleId, sourceId),
        RuleId = ruleId,
        Category = RiskCategory.Configuration,
        Severity = severity,
        Confidence = Confidence.High(),
        Title = "Test",
        Description = "Test",
        SourceEntityId = sourceId,
        Evidence = [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.ConfigurationFile, Location = sourceId }],
        Recommendation = "Test"
    };

    [Fact]
    public void Build_BoundaryWithFindings_ProducesCorrectSummary()
    {
        var boundary = MakeBoundary("b1", "App One");
        var findings = new Dictionary<string, List<RiskFinding>>
        {
            ["b1"] = [MakeFinding("R1", "e1", RiskSeverity.High), MakeFinding("R2", "e2", RiskSeverity.Medium)]
        };

        var summaries = ApplicationRiskAggregator.Build(new Dictionary<string, ApplicationBoundary> { ["b1"] = boundary }, findings);

        var summary = Assert.Single(summaries);
        Assert.Equal("b1", summary.ApplicationBoundaryId);
        Assert.Equal("App One", summary.ApplicationBoundaryName);
        Assert.Equal(AggregateSeverity.High, summary.OverallSeverity);
        Assert.Equal(2, summary.TotalFindingCount);
        Assert.Equal(1, summary.AffectedBoundaryCount);
    }

    [Fact]
    public void Build_BoundaryWithZeroFindings_ProducesNoSummaryAtAll()
    {
        var boundary = MakeBoundary("b1", "App One");
        var findings = new Dictionary<string, List<RiskFinding>> { ["b1"] = [] };

        var summaries = ApplicationRiskAggregator.Build(new Dictionary<string, ApplicationBoundary> { ["b1"] = boundary }, findings);

        Assert.Empty(summaries); // never an empty/None summary — no entry at all
    }

    [Fact]
    public void Build_UnknownBoundaryId_FallsBackToIdAsName()
    {
        var findings = new Dictionary<string, List<RiskFinding>> { ["unknown-id"] = [MakeFinding("R1", "e1")] };

        var summaries = ApplicationRiskAggregator.Build(new Dictionary<string, ApplicationBoundary>(), findings);

        var summary = Assert.Single(summaries);
        Assert.Equal("unknown-id", summary.ApplicationBoundaryName);
    }

    [Fact]
    public void Build_MultipleBoundaries_OrderedDeterministically_ByIdOrdinal()
    {
        var boundaries = new Dictionary<string, ApplicationBoundary>
        {
            ["b-zebra"] = MakeBoundary("b-zebra", "Zebra"),
            ["b-alpha"] = MakeBoundary("b-alpha", "Alpha")
        };
        var findings = new Dictionary<string, List<RiskFinding>>
        {
            ["b-zebra"] = [MakeFinding("R1", "e1")],
            ["b-alpha"] = [MakeFinding("R2", "e2")]
        };

        var summaries = ApplicationRiskAggregator.Build(boundaries, findings);

        Assert.Equal(["b-alpha", "b-zebra"], summaries.Select(s => s.ApplicationBoundaryId));
    }
}
