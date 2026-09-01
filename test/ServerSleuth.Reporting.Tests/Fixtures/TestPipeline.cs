using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Aggregation;
using ServerSleuth.Analysis.Risk.Engine;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Reporting.Tests.Fixtures;

/// <summary>Runs the full Discovery(synthetic)→Correlation→Boundary→Expansion→Validation→Risk→
/// Migration Assessment→Migration Plan→Consolidation pipeline against a synthetic entity list —
/// mirrors <c>ServerSleuth.Analysis.Tests.Risk.RiskPipeline</c>'s own role, extended all the way
/// to Phase 8C's <see cref="ServerMigrationAssessmentReport"/> so Reporting tests exercise the
/// real, actually-produced shape rather than a hand-built one.</summary>
internal static class TestPipeline
{
    private static readonly IReadOnlyList<IRiskRule> AllRules =
    [
        new MissingDependencyRule(),
        new MissingBinaryRule(),
        new AccessDeniedRule(),
        new MissingRuntimeRule(),
        new CertificateExpiryRule(),
        new ServiceDependencyRule(),
        new ScheduledTaskDependencyRule(),
        new ComDependencyRule(),
        new ExternalDependencyRule(),
        new SharedInfrastructureRule(),
        new ConfigurationRiskRule(),
        new GraphIntegrityRule()
    ];

    public static ServerMigrationAssessmentReport Run(List<DiscoveryEntity> entities, AggregateDiscoveryResult? discovery = null)
    {
        var correlation = new CorrelationEngine().Correlate(entities);
        var boundaryResult = new ApplicationBoundaryEngine().Analyze(entities, correlation.Graph);
        var expansion = new DependencyExpansionEngine().Expand(entities, correlation.Graph, boundaryResult.Boundaries);
        var validation = new GraphValidator().Validate(entities, expansion, boundaryResult.Boundaries);

        var context = new RiskAnalysisContext(entities, expansion.ExpandedGraph, boundaryResult, expansion, validation);
        var riskResult = new RiskRuleEngine(AllRules).Analyze(context);
        var aggregation = new RiskAggregator().Aggregate(context, riskResult);
        var assessment = new MigrationAssessmentEngine().Assess(context, riskResult, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        return ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan, discovery);
    }

    /// <summary>
    /// GUI-8C — same pipeline as <see cref="Run"/>, but also returns the
    /// <see cref="AggregateDiscoveryResult"/> (synthesized from <paramref name="entities"/> when
    /// the caller does not already have a real one) and the boundaries produced by correlation,
    /// so HTML inventory-section tests can drive the real
    /// <c>HtmlReportRenderer(discovery, boundaries, externalDependencies)</c> overload with
    /// actually-discovered entities rather than fabricated ones.
    /// </summary>
    public static (ServerMigrationAssessmentReport Report, AggregateDiscoveryResult Discovery, IReadOnlyList<ApplicationBoundary> Boundaries)
        RunWithInventory(List<DiscoveryEntity> entities, AggregateDiscoveryResult? discovery = null)
    {
        var correlation = new CorrelationEngine().Correlate(entities);
        var boundaryResult = new ApplicationBoundaryEngine().Analyze(entities, correlation.Graph);
        var expansion = new DependencyExpansionEngine().Expand(entities, correlation.Graph, boundaryResult.Boundaries);
        var validation = new GraphValidator().Validate(entities, expansion, boundaryResult.Boundaries);

        var context = new RiskAnalysisContext(entities, expansion.ExpandedGraph, boundaryResult, expansion, validation);
        var riskResult = new RiskRuleEngine(AllRules).Analyze(context);
        var aggregation = new RiskAggregator().Aggregate(context, riskResult);
        var assessment = new MigrationAssessmentEngine().Assess(context, riskResult, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        var effectiveDiscovery = discovery ?? new AggregateDiscoveryResult
        {
            Entities = entities,
            Errors = [],
            ScannerResults = [],
            ScannerStatuses = new Dictionary<string, ScannerStatus>()
        };

        var report = ServerMigrationAssessmentReportEngine.Build(context, aggregation, assessment, plan, effectiveDiscovery);

        return (report, effectiveDiscovery, boundaryResult.Boundaries);
    }
}
