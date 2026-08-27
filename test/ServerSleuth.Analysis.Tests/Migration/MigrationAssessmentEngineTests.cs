using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Analysis.Tests.Risk;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// End-to-end <see cref="MigrationAssessmentEngine"/> tests via the real Discovery→Correlation→
/// Boundary→Expansion→Validation→Risk→Aggregation→Migration pipeline. See skill.md (Phase 8A)
/// §1, §7-8, §13-15, §21.
/// </summary>
public class MigrationAssessmentEngineTests
{
    [Fact]
    public void FindingWithExplicitBoundary_RoutesItsMigrationIssueToThatApplicationAssessment()
    {
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");

        var entities = new List<DiscoveryEntity> { site, pool, app, config };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var migration = new MigrationAssessmentEngine().Assess(context, result, aggregation);

        var appAssessment = Assert.Single(migration.Server.ApplicationAssessments);
        Assert.Equal("boundary:iis-application:App1:/", appAssessment.ApplicationBoundaryId);
        Assert.Single(appAssessment.Issues);
        Assert.Equal("RR3-AccessDenied", appAssessment.Issues[0].RuleId);
    }

    [Fact]
    public void FindingResolvingToNoBoundary_StillVisibleInServerIssues_NeverDropped()
    {
        var expiring = EntityFactory.Certificate("orphan.example.com", "MIGORPHAN1", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var entities = new List<DiscoveryEntity> { expiring };

        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var migration = new MigrationAssessmentEngine().Assess(context, result, aggregation);

        Assert.NotEmpty(result.Findings);
        Assert.Empty(migration.Server.ApplicationAssessments);
        Assert.Equal(result.Findings.Count, migration.Server.Issues.Count);
        Assert.Equal(0, migration.Server.AffectedBoundaryCount);
    }

    [Fact]
    public void Diagnostics_ApplicationAssessmentsCreated_MatchesActualCount()
    {
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");
        var orphanCert = EntityFactory.Certificate("orphan2.example.com", "MIGORPHAN2", validTo: DateTimeOffset.UtcNow.AddDays(3));

        var entities = new List<DiscoveryEntity> { site, pool, app, config, orphanCert };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var migration = new MigrationAssessmentEngine().Assess(context, result, aggregation);

        Assert.Equal(migration.Server.ApplicationAssessments.Count, migration.Diagnostics.ApplicationAssessmentsCreated);

        // FindingsEvaluated/IssuesCreated count every MigrationPolicyEvaluator.Evaluate call —
        // once for the server-level pass over aggregation.Server.Findings, PLUS once more for
        // every (finding, boundary) pair evaluated while building each ApplicationAssessment.
        var expectedEvaluations = migration.Server.Issues.Count + migration.Server.ApplicationAssessments.Sum(a => a.Issues.Count);
        Assert.Equal(expectedEvaluations, migration.Diagnostics.FindingsEvaluated);
        Assert.Equal(expectedEvaluations, migration.Diagnostics.IssuesCreated);
    }

    [Fact]
    public void Assess_DoesNotMutateInputs()
    {
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");

        var entities = new List<DiscoveryEntity> { site, pool, app, config };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);

        var findingsCountBefore = result.Findings.Count;
        var boundariesCountBefore = context.Boundaries.Count;
        var serverFindingsCountBefore = aggregation.Server.Findings.Count;
        var firstFindingBefore = result.Findings[0];

        var migration = new MigrationAssessmentEngine().Assess(context, result, aggregation);

        Assert.Equal(findingsCountBefore, result.Findings.Count);
        Assert.Equal(boundariesCountBefore, context.Boundaries.Count);
        Assert.Equal(serverFindingsCountBefore, aggregation.Server.Findings.Count);
        Assert.Same(firstFindingBefore, result.Findings[0]);

        // The MigrationIssue's own Evidence is the exact same list reference the source
        // RiskFinding carries — never copied.
        var issue = migration.Server.Issues.Single(i => i.RuleId == "RR3-AccessDenied");
        var sourceFinding = result.Findings.Single(f => f.Id == issue.SourceRiskFindingId);
        Assert.Same(sourceFinding.Evidence, issue.Evidence);
    }

    [Fact]
    public void Assess_RepeatedRuns_ProduceIdenticalResults_Deterministic()
    {
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");

        var entities = new List<DiscoveryEntity> { site, pool, app, config };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);
        var engine = new MigrationAssessmentEngine();

        var runA = engine.Assess(context, result, aggregation);
        var runB = engine.Assess(context, result, aggregation);

        Assert.Equal(runA.Server.OverallStatus, runB.Server.OverallStatus);
        Assert.Equal(runA.Server.Issues.Select(i => i.IssueId), runB.Server.Issues.Select(i => i.IssueId));
        Assert.Equal(runA.Server.ApplicationAssessments.Select(a => a.ApplicationBoundaryId), runB.Server.ApplicationAssessments.Select(a => a.ApplicationBoundaryId));
    }
}
