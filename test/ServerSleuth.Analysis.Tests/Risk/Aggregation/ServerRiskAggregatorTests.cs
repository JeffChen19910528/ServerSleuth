using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>See skill.md (Phase 7B) §5, §13.</summary>
public class ServerRiskAggregatorTests
{
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

    private static ApplicationRiskSummary MakeAppSummary(string id, RiskFinding finding) => new()
    {
        ApplicationBoundaryId = id,
        ApplicationBoundaryName = id,
        OverallSeverity = finding.Severity.ToAggregateSeverity(),
        CriticalCount = finding.Severity == RiskSeverity.Critical ? 1 : 0,
        HighCount = finding.Severity == RiskSeverity.High ? 1 : 0,
        MediumCount = finding.Severity == RiskSeverity.Medium ? 1 : 0,
        LowCount = finding.Severity == RiskSeverity.Low ? 1 : 0,
        InfoCount = finding.Severity == RiskSeverity.Info ? 1 : 0,
        TotalFindingCount = 1,
        AffectedEntityCount = 1,
        AffectedBoundaryCount = 1,
        Findings = [finding],
        TopRisks = [finding],
        CategoryCounts = new Dictionary<RiskCategory, int> { [finding.Category] = 1 },
        SharedDependencyCount = 0,
        AggregateConfidence = finding.Confidence
    };

    [Fact]
    public void Build_CoversAppScopedAndServerScopedFindingsTogether()
    {
        var appFinding = MakeFinding("R1", "e1", RiskSeverity.High);
        var serverFinding = MakeFinding("R2", "e2", RiskSeverity.Critical);
        var appSummary = MakeAppSummary("b1", appFinding);

        var server = ServerRiskAggregator.Build([appFinding, serverFinding], [appSummary], serverScopedFindingCount: 1);

        Assert.Equal(2, server.TotalFindingCount);
        Assert.Equal(AggregateSeverity.Critical, server.OverallSeverity); // the server-scoped Critical still dominates
        Assert.Contains(appFinding, server.Findings);
        Assert.Contains(serverFinding, server.Findings);
    }

    [Fact]
    public void Build_ServerScopedFindingCount_ReportedAsGiven()
    {
        var finding = MakeFinding("R1", "e1");
        var server = ServerRiskAggregator.Build([finding], [], serverScopedFindingCount: 1);

        Assert.Equal(1, server.ServerScopedFindingCount);
    }

    [Fact]
    public void Build_AffectedBoundaryCount_EqualsApplicationSummaryCount()
    {
        var f1 = MakeFinding("R1", "e1");
        var f2 = MakeFinding("R2", "e2");
        var summaries = new[] { MakeAppSummary("b1", f1), MakeAppSummary("b2", f2) };

        var server = ServerRiskAggregator.Build([f1, f2], summaries, serverScopedFindingCount: 0);

        Assert.Equal(2, server.AffectedBoundaryCount);
        Assert.Equal(2, server.ApplicationSummaries.Count);
    }
}
