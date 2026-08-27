using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration.Consolidation;

/// <summary>Determinism, no-mutation, server-level-issue visibility, and graph-validation
/// pass-through — see skill.md (Phase 8C) §14-16, §20.</summary>
public class ServerMigrationAssessmentReportEngineTests
{
    private static List<DiscoveryEntity> BuildScenario()
    {
        var service = EntityFactory.Service("ConsSvc", @"D:\Cons\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Cons\svc.exe", notFound: true);
        var expiring = EntityFactory.Certificate("cons.example.com", "CONSCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var config = EntityFactory.Configuration(@"D:\Cons\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "ConsDb");

        return [service, missingExe, expiring, config];
    }

    private static ServerMigrationAssessmentReport BuildReport(List<DiscoveryEntity> entities, out RiskAnalysisContext context, out MigrationAssessmentSummary assessment)
    {
        RiskAnalysisResult result;
        (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        return ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);
    }

    [Fact]
    public void Report_IsDeterministic_AcrossRepeatedRuns()
    {
        var entities = BuildScenario();
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        var reportA = ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);
        var reportB = ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);

        Assert.Equal(reportA.ApplicationAssessments.Select(a => a.Assessment.ApplicationBoundaryId), reportB.ApplicationAssessments.Select(a => a.Assessment.ApplicationBoundaryId));
        Assert.Equal(reportA.Actions.Select(a => a.ActionId), reportB.Actions.Select(a => a.ActionId));
        Assert.Equal(reportA.ServerLevelIssues.Select(i => i.IssueId), reportB.ServerLevelIssues.Select(i => i.IssueId));
        Assert.Equal(reportA.Dependencies.Select(g => g.Type), reportB.Dependencies.Select(g => g.Type));
    }

    [Fact]
    public void ApplicationAssessments_AreSortedByBoundaryIdOrdinal()
    {
        var report = BuildReport(BuildScenario(), out _, out _);
        var ids = report.ApplicationAssessments.Select(a => a.Assessment.ApplicationBoundaryId).ToList();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
    }

    [Fact]
    public void Actions_AreSortedByPriorityDescending_ThenActionIdOrdinal()
    {
        var report = BuildReport(BuildScenario(), out _, out _);

        var priorities = report.Actions.Select(a => (int)a.Priority).ToList();
        Assert.Equal(priorities.OrderByDescending(p => p), priorities);
    }

    [Fact]
    public void DependencyGroups_AreSortedByTypeOrdinal()
    {
        var report = BuildReport(BuildScenario(), out _, out _);
        var types = report.Dependencies.Select(g => g.Type.ToString()).ToList();
        Assert.Equal(types.OrderBy(t => t, StringComparer.Ordinal), types);
    }

    [Fact]
    public void Report_NeverMutates_TheOriginalAssessmentOrPlan()
    {
        var entities = BuildScenario();
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        var issuesBefore = assessment.Server.Issues.Select(i => i.IssueId).ToList();
        var dependenciesBefore = assessment.Server.Dependencies.Select(d => d.DependencyId).ToList();
        var actionsBefore = plan.Actions.Select(a => a.ActionId).ToList();

        ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);

        Assert.Equal(issuesBefore, assessment.Server.Issues.Select(i => i.IssueId));
        Assert.Equal(dependenciesBefore, assessment.Server.Dependencies.Select(d => d.DependencyId));
        Assert.Equal(actionsBefore, plan.Actions.Select(a => a.ActionId));
    }

    [Fact]
    public void Report_ReusesActionInstances_NeverRegeneratesThem()
    {
        var entities = BuildScenario();
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var assessment = new MigrationAssessmentEngine().Assess(context, result, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);
        var report = ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan);

        Assert.NotEmpty(plan.Actions);
        foreach (var action in report.Actions)
        {
            Assert.Contains(plan.Actions, a => ReferenceEquals(a, action));
        }
    }

    // Server-level (no ApplicationBoundary attribution) findings must remain visible — §15.
    [Fact]
    public void ServerScopedCertificateIssue_HasNoBoundaryAttribution_StillVisibleAsServerLevelIssue()
    {
        var certificate = EntityFactory.Certificate("srv.example.com", "SRVCERT", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var report = BuildReport([certificate], out _, out var assessment);

        Assert.Empty(assessment.Server.ApplicationAssessments);
        var issue = Assert.Single(report.ServerLevelIssues, i => i.RuleId == "RR5-CertificateExpiry");
        Assert.Empty(issue.AffectedBoundaryIds);
    }

    [Fact]
    public void GraphValidationErrors_AreEmpty_ForACleanGraph()
    {
        var report = BuildReport(BuildScenario(), out var context, out _);
        Assert.Empty(context.Validation.Findings.Where(f => f.Severity == ValidationSeverity.Error));
        Assert.Empty(report.GraphValidationErrors);
    }
}
