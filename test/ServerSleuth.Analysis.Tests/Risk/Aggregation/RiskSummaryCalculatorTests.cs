using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>
/// Unit tests for <see cref="RiskSummaryCalculator"/> — the single shared implementation behind
/// both <see cref="ApplicationRiskAggregator"/> and <see cref="ServerRiskAggregator"/>. See
/// skill.md (Phase 7B) §2, §6, §8-12, §20.
/// </summary>
public class RiskSummaryCalculatorTests
{
    private static RiskFinding MakeFinding(
        string ruleId = "RR1",
        string sourceId = "entity:1",
        RiskSeverity severity = RiskSeverity.Medium,
        RiskCategory category = RiskCategory.Configuration,
        double confidence = 0.8,
        IReadOnlyList<string>? relatedIds = null) => new()
    {
        Id = RiskFinding.ComputeId(ruleId, sourceId, relatedIds),
        RuleId = ruleId,
        Category = category,
        Severity = severity,
        Confidence = new Confidence(confidence),
        Title = "Test finding",
        Description = "Test description",
        SourceEntityId = sourceId,
        RelatedEntityIds = relatedIds ?? [],
        Evidence = [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.ConfigurationFile, Location = sourceId }],
        Recommendation = "Test recommendation"
    };

    // --- ComputeOverallSeverity: skill.md (Phase 7B) §2, §20 escalation table ---

    [Fact]
    public void ComputeOverallSeverity_NoFindings_IsNone() =>
        Assert.Equal(AggregateSeverity.None, RiskSummaryCalculator.ComputeOverallSeverity([]));

    [Fact]
    public void ComputeOverallSeverity_OnlyInfo_IsInfo() =>
        Assert.Equal(AggregateSeverity.Info, RiskSummaryCalculator.ComputeOverallSeverity([MakeFinding(severity: RiskSeverity.Info)]));

    [Fact]
    public void ComputeOverallSeverity_OnlyLow_IsLow() =>
        Assert.Equal(AggregateSeverity.Low, RiskSummaryCalculator.ComputeOverallSeverity([MakeFinding(severity: RiskSeverity.Low)]));

    [Fact]
    public void ComputeOverallSeverity_MediumPlusLow_IsMedium() =>
        Assert.Equal(AggregateSeverity.Medium, RiskSummaryCalculator.ComputeOverallSeverity(
        [
            MakeFinding("R1", "e1", RiskSeverity.Medium),
            MakeFinding("R2", "e2", RiskSeverity.Low)
        ]));

    [Fact]
    public void ComputeOverallSeverity_HighPlusMedium_IsHigh() =>
        Assert.Equal(AggregateSeverity.High, RiskSummaryCalculator.ComputeOverallSeverity(
        [
            MakeFinding("R1", "e1", RiskSeverity.High),
            MakeFinding("R2", "e2", RiskSeverity.Medium)
        ]));

    [Fact]
    public void ComputeOverallSeverity_CriticalPlusAnything_IsCritical() =>
        Assert.Equal(AggregateSeverity.Critical, RiskSummaryCalculator.ComputeOverallSeverity(
        [
            MakeFinding("R1", "e1", RiskSeverity.Info),
            MakeFinding("R2", "e2", RiskSeverity.Low),
            MakeFinding("R3", "e3", RiskSeverity.Medium),
            MakeFinding("R4", "e4", RiskSeverity.High),
            MakeFinding("R5", "e5", RiskSeverity.Critical)
        ]));

    // --- ComputeAffectedEntityCount ---

    [Fact]
    public void ComputeAffectedEntityCount_DeduplicatesAcrossSourceAndRelatedIds()
    {
        var findings = new[]
        {
            MakeFinding("R1", "e1", relatedIds: ["e2", "e3"]),
            MakeFinding("R2", "e3", relatedIds: ["e4"]) // e3 overlaps with R1's related id
        };

        Assert.Equal(4, RiskSummaryCalculator.ComputeAffectedEntityCount(findings)); // e1,e2,e3,e4
    }

    // --- ComputeCategoryCounts ---

    [Fact]
    public void ComputeCategoryCounts_GroupsByCategory_OmitsZeroCategories()
    {
        var findings = new[]
        {
            MakeFinding("R1", "e1", category: RiskCategory.Certificate),
            MakeFinding("R2", "e2", category: RiskCategory.Certificate),
            MakeFinding("R3", "e3", category: RiskCategory.MissingBinary)
        };

        var counts = RiskSummaryCalculator.ComputeCategoryCounts(findings);

        Assert.Equal(2, counts[RiskCategory.Certificate]);
        Assert.Equal(1, counts[RiskCategory.MissingBinary]);
        Assert.False(counts.ContainsKey(RiskCategory.ExternalDependency));
    }

    // --- ComputeSharedDependencyCount ---

    [Fact]
    public void ComputeSharedDependencyCount_CountsOnlySharedInfrastructureCategory()
    {
        var findings = new[]
        {
            MakeFinding("R1", "e1", category: RiskCategory.SharedInfrastructure),
            MakeFinding("R2", "e2", category: RiskCategory.SharedInfrastructure),
            MakeFinding("R3", "e3", category: RiskCategory.Certificate)
        };

        Assert.Equal(2, RiskSummaryCalculator.ComputeSharedDependencyCount(findings));
    }

    // --- ComputeAggregateConfidence: skill.md (Phase 7B) §10 — max, never sum/average ---

    [Fact]
    public void ComputeAggregateConfidence_Empty_IsZero() =>
        Assert.Equal(0.0, RiskSummaryCalculator.ComputeAggregateConfidence([]).Value);

    [Fact]
    public void ComputeAggregateConfidence_IsMaxOfContributingFindings_NeverSumOrAverage()
    {
        var findings = new[]
        {
            MakeFinding("R1", "e1", confidence: 0.30),
            MakeFinding("R2", "e2", confidence: 0.35),
            MakeFinding("R3", "e3", confidence: 0.40)
        };

        var aggregate = RiskSummaryCalculator.ComputeAggregateConfidence(findings);

        // If this were averaged/summed it would be ~0.35 or >1.0 respectively — it must be
        // exactly the single strongest contributing finding's confidence, 0.40.
        Assert.Equal(0.40, aggregate.Value, precision: 6);
    }

    [Fact]
    public void ComputeAggregateConfidence_OneHighConfidenceFinding_IsNotDilutedByManyLowOnes()
    {
        var findings = new List<RiskFinding> { MakeFinding("R0", "e0", confidence: 0.95) };
        for (var i = 1; i <= 20; i++)
        {
            findings.Add(MakeFinding($"R{i}", $"e{i}", confidence: 0.10));
        }

        Assert.Equal(0.95, RiskSummaryCalculator.ComputeAggregateConfidence(findings).Value, precision: 6);
    }

    // --- ComputeTopRisks: skill.md (Phase 7B) §11 ordering cascade ---

    [Fact]
    public void ComputeTopRisks_OrdersBySeverityDescendingFirst()
    {
        var findings = new[]
        {
            MakeFinding("R1", "e1", RiskSeverity.Low),
            MakeFinding("R2", "e2", RiskSeverity.Critical),
            MakeFinding("R3", "e3", RiskSeverity.Medium)
        };

        var top = RiskSummaryCalculator.ComputeTopRisks(findings);

        Assert.Equal(["R2", "R3", "R1"], top.Select(f => f.RuleId));
    }

    [Fact]
    public void ComputeTopRisks_TiesOnSeverity_BrokenByImpactDescending()
    {
        var lowImpact = MakeFinding("R1", "e1", RiskSeverity.High, relatedIds: []);
        var highImpact = MakeFinding("R2", "e2", RiskSeverity.High, relatedIds: ["e3", "e4", "e5"]);

        var top = RiskSummaryCalculator.ComputeTopRisks([lowImpact, highImpact]);

        Assert.Equal("R2", top[0].RuleId); // higher impact (4 distinct entities) ranks first
        Assert.Equal("R1", top[1].RuleId);
    }

    [Fact]
    public void ComputeTopRisks_TiesOnSeverityAndImpact_BrokenByConfidenceDescending()
    {
        var lowConfidence = MakeFinding("R1", "e1", RiskSeverity.High, confidence: 0.60);
        var highConfidence = MakeFinding("R2", "e2", RiskSeverity.High, confidence: 0.90);

        var top = RiskSummaryCalculator.ComputeTopRisks([lowConfidence, highConfidence]);

        Assert.Equal("R2", top[0].RuleId);
        Assert.Equal("R1", top[1].RuleId);
    }

    [Fact]
    public void ComputeTopRisks_TiesOnEverything_BrokenByRuleIdThenFindingIdOrdinal()
    {
        var findingB = MakeFinding("RRB", "e2", RiskSeverity.High, confidence: 0.80);
        var findingA = MakeFinding("RRA", "e1", RiskSeverity.High, confidence: 0.80);

        var top = RiskSummaryCalculator.ComputeTopRisks([findingB, findingA]);

        Assert.Equal("RRA", top[0].RuleId); // "RRA" < "RRB" ordinally
        Assert.Equal("RRB", top[1].RuleId);
    }

    [Fact]
    public void ComputeTopRisks_CapsAtTopRisksLimit()
    {
        var findings = Enumerable.Range(0, RiskSummaryCalculator.TopRisksLimit + 5)
            .Select(i => MakeFinding($"R{i}", $"e{i}", RiskSeverity.Medium))
            .ToList();

        var top = RiskSummaryCalculator.ComputeTopRisks(findings);

        Assert.Equal(RiskSummaryCalculator.TopRisksLimit, top.Count);
    }
}
