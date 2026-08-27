using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

/// <summary>
/// GraphIntegrityRule reads only <see cref="RiskAnalysisContext.Validation"/> — these tests
/// build a <see cref="GraphValidationResult"/> directly (mirroring
/// GraphValidatorNodeEdgeTests' pattern) rather than going through the full
/// Discovery→Correlation→Boundary→Expansion pipeline, since a real CorrelationEngine run never
/// itself produces a dangling edge to validate against.
/// </summary>
public class GraphIntegrityRuleTests
{
    private static DependencyExpansionResult Wrap(DependencyGraph graph) => new()
    {
        ExternalDependencies = [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    private static RiskAnalysisContext BuildContext(IReadOnlyList<DiscoveryEntity> entities, DependencyGraph graph, GraphValidationResult validation) =>
        new(entities, graph, new BoundaryAnalysisResult { Boundaries = [], Diagnostics = new BoundaryDiagnostics() }, Wrap(graph), validation);

    [Fact]
    public void DanglingTargetEdge_ErrorSeverity_ProducesGraphIntegrityFinding()
    {
        var source = EntityFactory.Application("ERP", "/", @"D:\ERP");
        var graph = new DependencyGraph();
        graph.AddNode(source);
        graph.AddEdge(new DependencyEdge
        {
            SourceEntityId = source.Id,
            TargetEntityId = "does-not-exist",
            Type = DependencyEdgeType.Hosts,
            Confidence = Confidence.VeryHigh(),
            Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "x" }]
        });

        var entities = new List<DiscoveryEntity> { source };
        var validation = new GraphValidator().Validate(entities, Wrap(graph), []);
        Assert.Contains(validation.Findings, f => f.Code == "DanglingTarget" && f.Severity == ValidationSeverity.Error);

        var context = BuildContext(entities, graph, validation);
        var findings = new GraphIntegrityRule().Evaluate(context);

        var finding = Assert.Single(findings, f => f.Metadata.GetValueOrDefault("ValidationCode") == "DanglingTarget");
        Assert.Equal(RiskCategory.GraphIntegrity, finding.Category);
        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.NotEmpty(finding.Evidence);
    }

    [Fact]
    public void OnlyWarningOrInfoValidationFindings_NeverProduceGraphIntegrityFindings()
    {
        // Two Uses edges with identical shape between the same nodes trip DuplicateDetection's
        // Warning-level path, not Error — confirm it is never surfaced as a migration risk.
        var a = EntityFactory.Application("A", "/", @"D:\A");
        var b = EntityFactory.Application("B", "/", @"D:\B");
        var graph = new DependencyGraph();
        graph.AddNode(a);
        graph.AddNode(b);
        graph.AddEdge(new DependencyEdge { SourceEntityId = a.Id, TargetEntityId = b.Id, Type = DependencyEdgeType.Uses, Confidence = Confidence.High(), Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "x" }] });
        graph.AddEdge(new DependencyEdge { SourceEntityId = a.Id, TargetEntityId = b.Id, Type = DependencyEdgeType.Uses, Confidence = Confidence.VeryHigh(), Evidence = [new EvidenceRecord { Type = EvidenceType.IisConfiguration, Location = "y" }] });

        var entities = new List<DiscoveryEntity> { a, b };
        var validation = new GraphValidator().Validate(entities, Wrap(graph), []);
        Assert.DoesNotContain(validation.Findings, f => f.Severity == ValidationSeverity.Error);

        var context = BuildContext(entities, graph, validation);
        var findings = new GraphIntegrityRule().Evaluate(context);

        Assert.Empty(findings);
    }

    [Fact]
    public void NoValidationFindingsAtAll_ProducesNoRiskFindings()
    {
        var entity = EntityFactory.Application("Clean", "/", @"D:\Clean");
        var graph = new DependencyGraph();
        graph.AddNode(entity);

        var entities = new List<DiscoveryEntity> { entity };
        var validation = new GraphValidator().Validate(entities, Wrap(graph), []);

        var context = BuildContext(entities, graph, validation);
        var findings = new GraphIntegrityRule().Evaluate(context);

        Assert.Empty(findings);
    }
}
