using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>End-to-end <see cref="RiskAggregator"/> tests via the real
/// Discovery→Correlation→Boundary→Expansion→Validation→Risk→Aggregation pipeline. See skill.md
/// (Phase 7B) §1, §4-5, §7, §16.</summary>
public class RiskAggregatorTests
{
    [Fact]
    public void Aggregate_FindingWithExplicitApplicationBoundaryId_RoutesToThatBoundary()
    {
        // AccessDeniedRule stamps an explicit ApplicationBoundaryId when the entity is a
        // boundary member — see ErpRiskAggregationFixtureTests for the concrete case (RR3 on
        // web.config, explicit ApplicationBoundaryId == the IIS application boundary).
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");

        var entities = new List<DiscoveryEntity> { site, pool, app, config };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);

        Assert.NotEmpty(result.Findings);
        Assert.Single(aggregation.Server.ApplicationSummaries);
        Assert.Equal(0, aggregation.Server.ServerScopedFindingCount);
    }

    [Fact]
    public void Aggregate_FindingsResolvingToNoBoundary_AreServerScoped_NeverDropped()
    {
        // A certificate with no site binding at all is never a boundary member, so its
        // CertificateExpiryRule finding has no way to resolve to any application boundary.
        var expiring = EntityFactory.Certificate("orphan-expiring.example.com", "ORPHANEXP1", validTo: DateTimeOffset.UtcNow.AddDays(3));

        var entities = new List<DiscoveryEntity> { expiring };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);

        Assert.NotEmpty(result.Findings);
        Assert.Empty(aggregation.Server.ApplicationSummaries);
        Assert.Equal(result.Findings.Count, aggregation.Server.ServerScopedFindingCount);
        Assert.Equal(result.Findings.Count, aggregation.Server.TotalFindingCount); // still visible server-wide
    }

    [Fact]
    public void Aggregate_DiagnosticsAreInternallyConsistent()
    {
        var site = EntityFactory.Site("App1", @"D:\App1");
        var pool = EntityFactory.ApplicationPool("App1Pool");
        var app = EntityFactory.Application("App1", "/", @"D:\App1", poolId: pool.Id, siteId: site.Id);
        var config = EntityFactory.Configuration(@"D:\App1\web.config", ownerEntityId: app.Id);
        config.SetMetadata("ParseStatus", "AccessDenied");
        var orphanCert = EntityFactory.Certificate("orphan.example.com", "ORPHANEXP2", validTo: DateTimeOffset.UtcNow.AddDays(3));

        var entities = new List<DiscoveryEntity> { site, pool, app, config, orphanCert };
        var (result, context) = RiskPipeline.Run(entities);
        var aggregation = new RiskAggregator().Aggregate(context, result);

        Assert.Equal(result.Findings.Count, aggregation.Diagnostics.FindingsProcessed);
        Assert.Equal(aggregation.Server.ApplicationSummaries.Count, aggregation.Diagnostics.ApplicationSummariesCreated);
        Assert.Equal(aggregation.Server.ServerScopedFindingCount, aggregation.Diagnostics.ServerLevelFindingCount);
        Assert.Equal(aggregation.Diagnostics.ServerLevelFindingCount, aggregation.Diagnostics.UnresolvedOwnershipCount);
        Assert.Equal(aggregation.Server.TopRisks.Count, aggregation.Diagnostics.TopRisksSelected);
        Assert.Equal(result.Diagnostics.FindingsDeduplicated, aggregation.Diagnostics.FindingsDeduplicatedUpstream);
    }
}
