using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation;

/// <summary>
/// Negative fixture (skill.md §23): two DLLs share a filename under two unrelated application
/// roots. Only the application that actually references its own copy may receive an edge to
/// it — the other application's same-named DLL must never be linked in.
/// </summary>
public class NegativeFixtureTests
{
    [Fact]
    public void Correlate_TwoAppsWithSameNamedDll_OnlyReferencingAppGetsContainsEdge()
    {
        var appA = EntityFactory.Application("AppA", "/", @"C:\AppA");
        var appB = EntityFactory.Application("AppB", "/", @"C:\AppB");
        var dllInA = EntityFactory.Dll(@"C:\AppA\Vendor.dll", referencedBy: [appA.Id]);
        var dllInB = EntityFactory.Dll(@"C:\AppB\Vendor.dll"); // discovered, but not referenced by AppB

        var entities = new List<DiscoveryEntity> { appA, appB, dllInA, dllInB };
        var result = new CorrelationEngine().Correlate(entities);

        var containsEdges = result.Graph.Edges.Where(e => e.Type == DependencyEdgeType.Contains).ToList();
        var edge = Assert.Single(containsEdges);
        Assert.Equal(appA.Id, edge.SourceEntityId);
        Assert.Equal(dllInA.Id, edge.TargetEntityId);
        Assert.DoesNotContain(containsEdges, e => e.TargetEntityId == dllInB.Id);
        Assert.DoesNotContain(containsEdges, e => e.SourceEntityId == appB.Id);
    }

    [Fact]
    public void Correlate_SameDisplayNameDifferentIdentity_NeverTreatedAsSameEntity()
    {
        // Two application pools that happen to share a display Name but have distinct Ids
        // (identity must never be resolved by Name alone — skill.md §4).
        var poolProd = EntityFactory.ApplicationPool("SharedPoolName");
        var poolStaging = new ApplicationPool
        {
            Id = "iis-apppool:SharedPoolName-Staging",
            Name = "SharedPoolName",
            Type = "ApplicationPool",
            Source = "IisConfiguration",
            Confidence = Confidence.VeryHigh()
        };

        var app = EntityFactory.Application("ERP", "/", @"D:\ERP", poolId: poolProd.Id);

        var entities = new List<DiscoveryEntity> { poolProd, poolStaging, app };
        var result = new CorrelationEngine().Correlate(entities);

        var usesEdges = result.Graph.Edges.Where(e => e.Type == DependencyEdgeType.Uses).ToList();
        var edge = Assert.Single(usesEdges);
        Assert.Equal(poolProd.Id, edge.TargetEntityId);
        Assert.DoesNotContain(usesEdges, e => e.TargetEntityId == poolStaging.Id);
    }

    [Fact]
    public void Correlate_ComRegisteredWithNoApplicationEvidence_ProducesNoApplicationEdge()
    {
        var com = EntityFactory.Com("{ORPHAN-GUID}", inprocServer32: @"C:\Orphan\Orphan.dll");
        var orphanDll = EntityFactory.Dll(@"C:\Orphan\Orphan.dll"); // never referenced by any Application

        var entities = new List<DiscoveryEntity> { com, orphanDll };
        var result = new CorrelationEngine().Correlate(entities);

        Assert.DoesNotContain(result.Graph.Edges, e => e.Type == DependencyEdgeType.Contains);
        Assert.Contains(result.Graph.Edges, e => e.Type == DependencyEdgeType.References && e.SourceEntityId == com.Id);
    }
}
