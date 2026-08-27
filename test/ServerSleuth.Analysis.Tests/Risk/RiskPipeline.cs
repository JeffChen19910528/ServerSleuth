using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Boundaries;
using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Engine;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

/// <summary>Runs the full Discovery→Correlation→Boundary→Expansion→Validation→Risk pipeline
/// against a synthetic entity list, exactly mirroring what a real host would do — used so Risk
/// rule tests exercise the same shapes production analysis actually produces, never a hand-
/// built RiskAnalysisContext bypassing the real upstream engines.</summary>
internal static class RiskPipeline
{
    public static readonly IReadOnlyList<IRiskRule> AllRules =
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

    public static (RiskAnalysisResult Result, RiskAnalysisContext Context) Run(List<DiscoveryEntity> entities, IReadOnlyList<IRiskRule>? rules = null)
    {
        var correlation = new CorrelationEngine().Correlate(entities);
        var boundaryResult = new ApplicationBoundaryEngine().Analyze(entities, correlation.Graph);
        var expansion = new DependencyExpansionEngine().Expand(entities, correlation.Graph, boundaryResult.Boundaries);
        var validation = new GraphValidator().Validate(entities, expansion, boundaryResult.Boundaries);

        var context = new RiskAnalysisContext(entities, expansion.ExpandedGraph, boundaryResult, expansion, validation);
        var engine = new RiskRuleEngine(rules ?? AllRules);

        return (engine.Analyze(context), context);
    }
}
