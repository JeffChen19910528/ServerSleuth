using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Boundaries.Diagnostics;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Expansion.Diagnostics;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Graph;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// skill.md (Phase 8A) §19: Migration Assessment must not proceed as if the dependency graph is
/// trustworthy when Phase 5D reports an Error-severity integrity finding. Reuses the exact
/// dangling-edge construction from Phase 7A's `GraphIntegrityRuleTests` (a real CorrelationEngine
/// run never itself produces a dangling edge) to prove the Error flows all the way through
/// RR12-GraphIntegrity → Blocking MigrationIssue → Blocked MigrationStatus, WITHOUT
/// MigrationAssessmentEngine consuming GraphValidationResult a second time itself.
/// </summary>
public class GraphIntegrityMigrationInteractionTests
{
    private static DependencyExpansionResult Wrap(DependencyGraph graph) => new()
    {
        ExternalDependencies = [],
        ExpandedGraph = graph,
        DerivedWorkloadDependencies = [],
        Diagnostics = new ExpansionDiagnostics()
    };

    [Fact]
    public void DanglingTargetEdge_ErrorSeverity_ProducesBlockingMigrationIssue_AndBlocksTheAssessment()
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

        var boundaryResult = new BoundaryAnalysisResult { Boundaries = [], Diagnostics = new BoundaryDiagnostics() };
        var context = new RiskAnalysisContext(entities, graph, boundaryResult, Wrap(graph), validation);

        var findings = new GraphIntegrityRule().Evaluate(context);
        Assert.NotEmpty(findings);

        var analysisResult = new RiskAnalysisResult { Findings = findings, Diagnostics = new RiskDiagnostics() };
        var aggregation = new RiskAggregator().Aggregate(context, analysisResult);
        var migration = new MigrationAssessmentEngine().Assess(context, analysisResult, aggregation);

        var issue = Assert.Single(migration.Server.Issues, i => i.RuleId == "RR12-GraphIntegrity");
        Assert.Equal(MigrationStatusImpact.Blocking, issue.MigrationStatusImpact);
        Assert.Equal(MigrationStatus.Blocked, migration.Server.OverallStatus);
        Assert.Equal(1, migration.Server.BlockingIssueCount);
    }

    [Fact]
    public void NoIntegrityErrors_NeverBlocksTheAssessmentOnGraphGrounds()
    {
        var entity = EntityFactory.Application("Clean", "/", @"D:\Clean");
        var graph = new DependencyGraph();
        graph.AddNode(entity);

        var entities = new List<DiscoveryEntity> { entity };
        var validation = new GraphValidator().Validate(entities, Wrap(graph), []);
        Assert.DoesNotContain(validation.Findings, f => f.Severity == ValidationSeverity.Error);

        var boundaryResult = new BoundaryAnalysisResult { Boundaries = [], Diagnostics = new BoundaryDiagnostics() };
        var context = new RiskAnalysisContext(entities, graph, boundaryResult, Wrap(graph), validation);

        var findings = new GraphIntegrityRule().Evaluate(context);
        Assert.Empty(findings);

        var analysisResult = new RiskAnalysisResult { Findings = findings, Diagnostics = new RiskDiagnostics() };
        var aggregation = new RiskAggregator().Aggregate(context, analysisResult);
        var migration = new MigrationAssessmentEngine().Assess(context, analysisResult, aggregation);

        Assert.Equal(MigrationStatus.Ready, migration.Server.OverallStatus);
    }
}
