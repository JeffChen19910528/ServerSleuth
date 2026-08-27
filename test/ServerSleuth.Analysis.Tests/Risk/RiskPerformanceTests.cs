using System.Diagnostics;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Engine;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

/// <summary>
/// Synthetic large-graph performance validation (skill.md Phase 7A §34): >=5,000 entities,
/// >=10,000 edges, >=500 Configurations, >=500 binaries, >=200 Service/ScheduledTask, >=100
/// ExternalDependency. Built directly (mirroring GraphValidatorLargeGraphTests' pattern) rather
/// than through the full CorrelationEngine, since only RiskRuleEngine's own in-memory
/// performance is being measured here — no filesystem, registry, process, or network access
/// occurs anywhere in this test.
/// </summary>
public class RiskPerformanceTests
{
    [Fact]
    public void Analyze_5000EntitiesAnd10000Edges_CompletesInMemoryUnderTenSeconds()
    {
        var entities = new List<DiscoveryEntity>();
        var graph = new DependencyGraph();

        const int dllCount = 4000;
        var dlls = new Dll[dllCount];
        for (var i = 0; i < dllCount; i++)
        {
            var notFound = i % 10 == 0; // 250 missing binaries
            var dll = EntityFactory.Dll($@"D:\Synthetic\Binaries\Binary{i}.dll", notFound: notFound);
            dlls[i] = dll;
            entities.Add(dll);
            graph.AddNode(dll);
        }

        const int serviceCount = 150;
        for (var i = 0; i < serviceCount; i++)
        {
            var targetDll = dlls[i % dllCount];
            var service = EntityFactory.Service($"Service{i}", targetDll.Path);
            entities.Add(service);
            graph.AddNode(service);
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = service.Id,
                TargetEntityId = targetDll.Id,
                Type = DependencyEdgeType.Runs,
                Confidence = Confidence.VeryHigh(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.ServiceConfiguration, Location = service.Id }]
            });
        }

        const int taskCount = 60;
        for (var i = 0; i < taskCount; i++)
        {
            var targetDll = dlls[(i * 3) % dllCount];
            var task = EntityFactory.ScheduledTask($@"\Synthetic\Task{i}", targetDll.Path);
            entities.Add(task);
            graph.AddNode(task);
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = task.Id,
                TargetEntityId = targetDll.Id,
                Type = DependencyEdgeType.Runs,
                Confidence = Confidence.VeryHigh(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = task.Id }]
            });
        }

        const int externalDependencyCount = 120;
        var externalDependencies = new List<ExternalDependency>();
        for (var i = 0; i < externalDependencyCount; i++)
        {
            var dependency = EntityFactory.ExternalDependency(ExternalDependencyKinds.Database, $"database:synthetic:host{i}:1433:db{i}", $"host{i}");
            externalDependencies.Add(dependency);
            entities.Add(dependency);
            graph.AddNode(dependency);
        }

        const int configurationCount = 700;
        for (var i = 0; i < configurationCount; i++)
        {
            var refs = i % 3 == 0 ? new List<string> { "RuntimeVersion: net8.0" } : [];
            var config = EntityFactory.Configuration($@"D:\Synthetic\Configs\app{i}.config", dependencyReferences: refs);
            entities.Add(config);
            graph.AddNode(config);

            var dependency = externalDependencies[i % externalDependencyCount];
            graph.AddEdge(new DependencyEdge
            {
                SourceEntityId = config.Id,
                TargetEntityId = dependency.Id,
                Type = DependencyEdgeType.References,
                Confidence = Confidence.Medium(),
                Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = config.Id }]
            });
        }

        entities.Add(EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "6.0.0")); // net8.0 requirement above is never satisfied

        for (var i = 0; i < 50; i++)
        {
            var validTo = i % 5 == 0 ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddYears(1);
            var certificate = EntityFactory.Certificate($"synthetic-host{i}.example.com", $"THUMB{i}", validTo: validTo);
            entities.Add(certificate);
            graph.AddNode(certificate);
        }

        // Three independent DLL-to-DLL "fan" edge sets, each covering nearly every node, to
        // reach >=10,000 total edges while staying well-distributed (not one giant chain).
        var edgeCount = graph.Edges.Count;
        foreach (var multiplier in new[] { 7, 13, 19 })
        {
            for (var i = 0; i < dllCount && edgeCount < 10_500; i++)
            {
                var target = (i * multiplier) % dllCount;
                if (target == i)
                {
                    continue;
                }

                graph.AddEdge(new DependencyEdge
                {
                    SourceEntityId = dlls[i].Id,
                    TargetEntityId = dlls[target].Id,
                    Type = DependencyEdgeType.Imports,
                    Confidence = Confidence.High(),
                    Evidence = [new EvidenceRecord { Type = EvidenceType.PeMetadata, Location = dlls[i].Id }]
                });
                edgeCount++;
            }
        }

        Assert.True(entities.Count >= 5000, $"Expected >=5000 entities, got {entities.Count}");
        Assert.True(graph.Edges.Count >= 10_000, $"Expected >=10000 edges, got {graph.Edges.Count}");
        Assert.True(entities.OfType<Configuration>().Count() >= 500);
        Assert.True(entities.OfType<Dll>().Count() >= 500);
        Assert.True(entities.OfType<Service>().Count() + entities.OfType<ScheduledTask>().Count() >= 200);
        Assert.True(entities.OfType<ExternalDependency>().Count() >= 100);

        var expansion = new DependencyExpansionResult
        {
            ExternalDependencies = externalDependencies,
            ExpandedGraph = graph,
            DerivedWorkloadDependencies = [],
            Diagnostics = new ExpansionDiagnostics()
        };

        var validation = new GraphValidator().Validate(entities, expansion, []);
        var boundaryResult = new BoundaryAnalysisResult { Boundaries = [], Diagnostics = new BoundaryDiagnostics() };
        var context = new RiskAnalysisContext(entities, graph, boundaryResult, expansion, validation);

        var stopwatch = Stopwatch.StartNew();
        var result = new RiskRuleEngine(RiskPipeline.AllRules).Analyze(context);
        stopwatch.Stop();

        Assert.NotEmpty(result.Findings); // the missing binaries/certs/runtime requirement above must actually surface
        Assert.True(stopwatch.Elapsed.TotalSeconds < 10,
            $"RiskRuleEngine.Analyze over {entities.Count} entities / {graph.Edges.Count} edges took {stopwatch.Elapsed.TotalSeconds:0.00}s — expected well under 10s in-memory.");
    }
}
