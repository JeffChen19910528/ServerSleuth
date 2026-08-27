using ServerSleuth.Analysis.Correlation.Diagnostics;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// Orchestrates identity resolution and correlation rules over already-discovered entities,
/// producing an evidence-backed DependencyGraph — see skill.md §3. This is a pure analysis
/// step: it never re-invokes a scanner, executes a command, or touches the filesystem/network/
/// registry (skill.md §24-25). Given identical discovery input, output is deterministic
/// (skill.md §18): rule order is fixed, and rules only ever iterate the input lists supplied,
/// never a dictionary's enumeration order.
/// </summary>
public sealed class CorrelationEngine(IReadOnlyList<ICorrelationRule>? rules = null)
{
    private readonly IReadOnlyList<ICorrelationRule> _rules = rules ?? DefaultRules();

    public static IReadOnlyList<ICorrelationRule> DefaultRules() =>
    [
        new SiteHostsApplicationRule(),
        new ApplicationUsesApplicationPoolRule(),
        new ApplicationConfiguresConfigurationRule(),
        new ApplicationContainsBinaryRule(),
        new ServiceRunsBinaryRule(),
        new ScheduledTaskRunsBinaryRule(),
        new ComReferencesBinaryRule(),
        new IisBindingBindsToCertificateRule(),
        new BinaryImportsBinaryRule(),
        new ConfigurationReferencesRuntimeRule()
    ];

    public CorrelationResult Correlate(IReadOnlyList<DiscoveryResult> discoveryResults) =>
        Correlate(discoveryResults.SelectMany(r => r.Entities).ToList());

    public CorrelationResult Correlate(IReadOnlyList<DiscoveryEntity> entities)
    {
        var graph = new DependencyGraph();
        var addedIds = new HashSet<string>();

        foreach (var entity in entities)
        {
            if (addedIds.Add(entity.Id))
            {
                graph.AddNode(entity);
            }
        }

        var context = new CorrelationContext(entities);
        var diagnostics = new CorrelationDiagnostics();

        foreach (var rule in _rules)
        {
            foreach (var candidate in rule.Evaluate(context))
            {
                diagnostics.RecordEvaluated();
                ApplyCandidate(graph, context, diagnostics, candidate);
            }
        }

        return new CorrelationResult { Graph = graph, Diagnostics = diagnostics };
    }

    private static void ApplyCandidate(
        DependencyGraph graph,
        CorrelationContext context,
        CorrelationDiagnostics diagnostics,
        CorrelationCandidate candidate)
    {
        if (candidate.TargetEntityId is null)
        {
            diagnostics.RecordRejected(candidate.RuleId, candidate.SourceEntityId, null,
                candidate.UnresolvedReason ?? "Unresolved target");
            return;
        }

        if (!context.ById.ContainsKey(candidate.SourceEntityId))
        {
            diagnostics.RecordRejected(candidate.RuleId, candidate.SourceEntityId, candidate.TargetEntityId,
                "Source entity is not present in the discovery input");
            return;
        }

        if (!context.ById.ContainsKey(candidate.TargetEntityId))
        {
            diagnostics.RecordRejected(candidate.RuleId, candidate.SourceEntityId, candidate.TargetEntityId,
                "Target entity is not present in the discovery input");
            return;
        }

        if (candidate.SourceEntityId == candidate.TargetEntityId)
        {
            diagnostics.RecordRejected(candidate.RuleId, candidate.SourceEntityId, candidate.TargetEntityId,
                "Self-edge rejected");
            return;
        }

        if (candidate.Evidence.Count == 0)
        {
            diagnostics.RecordRejected(candidate.RuleId, candidate.SourceEntityId, candidate.TargetEntityId,
                "No evidence — no edge");
            return;
        }

        var edgeCountBefore = graph.Edges.Count;

        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = candidate.SourceEntityId,
            TargetEntityId = candidate.TargetEntityId,
            Type = candidate.Type,
            Confidence = candidate.Confidence,
            Evidence = candidate.Evidence
        });

        if (graph.Edges.Count > edgeCountBefore)
        {
            diagnostics.RecordEdgeCreated();
        }
        else
        {
            diagnostics.RecordDuplicateMerged();
        }
    }
}
