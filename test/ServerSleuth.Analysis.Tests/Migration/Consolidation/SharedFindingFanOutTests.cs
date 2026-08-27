using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Consolidation;

/// <summary>Shared-finding fan-out — see skill.md (Phase 8C) §6, §17: one logical risk finding
/// affecting 3 boundaries must appear as exactly one logical Issue/Dependency at the server level,
/// referenced (not copied) by each affected application.</summary>
public class SharedFindingFanOutTests
{
    private static ServerMigrationAssessmentReport BuildReport()
    {
        var serviceA = EntityFactory.Service("FanA", @"D:\Fan\host.exe");
        var serviceB = EntityFactory.Service("FanB", @"D:\Fan\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Fan\FanC", @"D:\Fan\host.exe");
        var exe = EntityFactory.Dll(@"D:\Fan\host.exe");

        var (result, context) = RiskPipeline.Run([serviceA, serviceB, taskC, exe]);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        return ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);
    }

    [Fact]
    public void ServerLevel_HasExactlyOneLogicalIssue()
    {
        var report = BuildReport();

        var issue = Assert.Single(report.Assessment.Server.Issues, i => i.RuleId == "RR10-SharedInfrastructure");
        Assert.Equal(3, issue.AffectedBoundaryIds.Count);
    }

    [Fact]
    public void EveryAffectedApplication_ReferencesTheSameLogicalIssue_NotACopy()
    {
        var report = BuildReport();

        // Phase 8A's MigrationPolicyEvaluator constructs one MigrationIssue instance per
        // Evaluate() call — one for the server-level view, one per affected application — so
        // these are distinct record instances that compare equal by value (never by reference).
        // "Not a copy" here means: same IssueId/SourceRiskFindingId (the same underlying
        // RiskFinding was never re-evaluated or duplicated), not CLR reference identity.
        var serverIssue = report.Assessment.Server.Issues.Single(i => i.RuleId == "RR10-SharedInfrastructure");
        Assert.Equal(3, report.ApplicationAssessments.Count);

        foreach (var app in report.ApplicationAssessments)
        {
            var appIssue = Assert.Single(app.Assessment.Issues);
            Assert.Equal(serverIssue.IssueId, appIssue.IssueId);
            Assert.Equal(serverIssue.SourceRiskFindingId, appIssue.SourceRiskFindingId);
            Assert.Equal(serverIssue.MigrationStatusImpact, appIssue.MigrationStatusImpact);
        }
    }

    [Fact]
    public void ServerSummary_AffectedBoundaryCount_IsThree()
    {
        var report = BuildReport();
        Assert.Equal(3, report.ServerSummary.AffectedBoundaryCount);
    }

    [Fact]
    public void SharedDependency_AppearsOnceInSharedInfrastructure_NeverPerBoundary()
    {
        var report = BuildReport();

        var dependency = Assert.Single(report.SharedInfrastructure);
        Assert.Equal(3, dependency.AffectedBoundaryIds.Count);
        Assert.Single(report.Assessment.Server.Dependencies);
    }

    [Fact]
    public void SharedAction_AppearsOnceAtTopLevel_ButIsReferencedByAllThreeApplications()
    {
        var report = BuildReport();

        Assert.Single(report.Actions);
        var action = report.Actions[0];
        Assert.Equal(3, action.AffectedBoundaryIds.Count);

        foreach (var app in report.ApplicationAssessments)
        {
            var appAction = Assert.Single(app.Actions);
            Assert.Same(action, appAction);
        }
    }
}
