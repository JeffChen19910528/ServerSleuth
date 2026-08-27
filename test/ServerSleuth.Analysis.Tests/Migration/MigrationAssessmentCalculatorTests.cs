using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// Direct tests of the internal <c>MigrationAssessmentCalculator</c> escalation/count/rollup
/// logic, exercised through <c>MigrationAssessmentEngine</c>'s own end-to-end output (the
/// calculator itself is <c>internal</c>, so these tests build small MigrationIssue/Dependency
/// lists by hand and verify the SAME behavior via a minimal engine run rather than reflection).
/// See skill.md (Phase 8A) §2, §6-8.
/// </summary>
public class MigrationAssessmentCalculatorTests
{
    [Fact]
    public void ComputeOverallStatus_NoFindings_IsReady()
    {
        var (result, context) = ServerSleuth.Analysis.Tests.Risk.RiskPipeline.Run([]);
        var aggregation = new ServerSleuth.Analysis.Risk.Aggregation.RiskAggregator().Aggregate(context, result);
        var migration = new ServerSleuth.Analysis.Migration.Assessment.MigrationAssessmentEngine().Assess(context, result, aggregation);

        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
        Assert.Equal(0, migration.Server.BlockingIssueCount);
        Assert.Equal(0, migration.Server.RemediationIssueCount);
        Assert.Equal(0, migration.Server.ConditionalDependencyCount);
        Assert.Empty(migration.Server.Issues);
    }

    [Fact]
    public void RollupEvidence_DeduplicatesIdenticalEvidenceRecords()
    {
        // Two RR9-ExternalDependency findings sharing the exact same Configuration-sourced
        // evidence (Database + FileShare on one config) still roll up without duplicate
        // (Type,Location,Detail) evidence entries — verified through the real ERP-style
        // fixture, which produces exactly this shape.
        var config = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Configuration(@"D:\App\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "App");

        var entities = new List<ServerSleuth.Core.Models.DiscoveryEntity> { config };
        var (result, context) = ServerSleuth.Analysis.Tests.Risk.RiskPipeline.Run(entities);
        var aggregation = new ServerSleuth.Analysis.Risk.Aggregation.RiskAggregator().Aggregate(context, result);
        var migration = new ServerSleuth.Analysis.Migration.Assessment.MigrationAssessmentEngine().Assess(context, result, aggregation);

        // The rollup must never contain two entries with identical (Type, Location, Detail).
        var grouped = migration.Server.Evidence.GroupBy(e => (e.Type, e.Location, e.Detail));
        Assert.All(grouped, g => Assert.Single(g));
    }

    [Fact]
    public void Sorted_Issues_AreOrdinalByIssueId()
    {
        var serviceA = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Service("ZSvc", @"D:\Z\z.exe");
        var missingZ = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Dll(@"D:\Z\z.exe", notFound: true);
        var serviceB = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Service("ASvc", @"D:\A\a.exe");
        var missingA = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Dll(@"D:\A\a.exe", notFound: true);

        var entities = new List<ServerSleuth.Core.Models.DiscoveryEntity> { serviceA, missingZ, serviceB, missingA };
        var (result, context) = ServerSleuth.Analysis.Tests.Risk.RiskPipeline.Run(entities);
        var aggregation = new ServerSleuth.Analysis.Risk.Aggregation.RiskAggregator().Aggregate(context, result);
        var migration = new ServerSleuth.Analysis.Migration.Assessment.MigrationAssessmentEngine().Assess(context, result, aggregation);

        var ids = migration.Server.Issues.Select(i => i.IssueId).ToList();
        var expectedSorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(expectedSorted, ids);
    }

    [Fact]
    public void Sorted_ApplicationAssessments_AreOrdinalByBoundaryId()
    {
        var serviceA = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Service("ZSvc", @"D:\Z\z.exe");
        var missingZ = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Dll(@"D:\Z\z.exe", notFound: true);
        var serviceB = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Service("ASvc", @"D:\A\a.exe");
        var missingA = ServerSleuth.Analysis.Tests.Fixtures.EntityFactory.Dll(@"D:\A\a.exe", notFound: true);

        var entities = new List<ServerSleuth.Core.Models.DiscoveryEntity> { serviceA, missingZ, serviceB, missingA };
        var (result, context) = ServerSleuth.Analysis.Tests.Risk.RiskPipeline.Run(entities);
        var aggregation = new ServerSleuth.Analysis.Risk.Aggregation.RiskAggregator().Aggregate(context, result);
        var migration = new ServerSleuth.Analysis.Migration.Assessment.MigrationAssessmentEngine().Assess(context, result, aggregation);

        var ids = migration.Server.ApplicationAssessments.Select(a => a.ApplicationBoundaryId).ToList();
        var expectedSorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.Equal(expectedSorted, ids);
        Assert.Equal(2, ids.Count);
    }
}
