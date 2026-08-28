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
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Analysis.Orchestration;

/// <summary>
/// Wires the existing Discovery → Correlation → Boundary → Expansion → Validation → Risk →
/// Aggregation → Migration Assessment → Migration Plan → Consolidation pipeline in the exact
/// conceptual order skill.md (Phase 10A) §7 specifies — every stage is an unmodified call into
/// an existing engine; this type reimplements no business rule and reorders nothing.
///
/// Relocated here from <c>ServerSleuth.Cli.Pipeline</c> in Phase GUI-3: this type only ever
/// depended on <c>ServerSleuth.Core</c>/<c>ServerSleuth.Analysis</c> types (never anything
/// Cli/Infrastructure/Windows/Reporting-specific), so moving it into
/// <c>ServerSleuth.Analysis</c> changes no dependency direction — it simply lets both
/// <c>ServerSleuth.Cli</c> (<see cref="ServerSleuth.Cli"/> already references
/// <c>ServerSleuth.Analysis</c>) and the new GUI execution host share the literal same
/// orchestration code, which is what makes CLI/GUI pipeline-semantic equivalence a structural
/// guarantee rather than something merely tested for.
/// </summary>
public sealed class ScanPipelineRunner(IDiscoveryEngine discoveryEngine)
{
    /// <summary>The same 12 <see cref="IRiskRule"/> implementations every prior phase's own
    /// pipeline (e.g. <c>RiskPipeline</c> in the Analysis/Reporting test suites) uses — never a
    /// second, caller-specific rule set.</summary>
    private static readonly IReadOnlyList<IRiskRule> RiskRules =
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

    public Task<AggregateDiscoveryResult> DiscoverAsync(DiscoveryContext context, CancellationToken cancellationToken) =>
        discoveryEngine.RunAsync(context, cancellationToken);

    /// <summary>Runs every post-discovery stage synchronously (each stage is pure, in-memory,
    /// CPU-bound computation over already-collected entities — none of it does its own I/O) and
    /// builds the final <see cref="ServerMigrationAssessmentReport"/>.
    ///
    /// <paramref name="onStageStarting"/> is an additive, optional (default <c>null</c>) Phase
    /// GUI-3 seam: when supplied, it is invoked immediately before each of the four coarse
    /// stages below actually runs — purely an observation hook, never altering which engine
    /// runs, in what order, or with what input. Every pre-existing caller (the CLI's own
    /// <c>ScanCommand</c>) keeps calling the original two-argument overload unchanged.</summary>
    public ScanPipelineResult Analyze(AggregateDiscoveryResult discovery, CancellationToken cancellationToken, Action<PipelineStage>? onStageStarting = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entities = discovery.Entities;

        onStageStarting?.Invoke(PipelineStage.Analysis);
        var correlation = new CorrelationEngine().Correlate(entities);
        var boundaryResult = new ApplicationBoundaryEngine().Analyze(entities, correlation.Graph);
        var expansion = new DependencyExpansionEngine().Expand(entities, correlation.Graph, boundaryResult.Boundaries);
        var validation = new GraphValidator().Validate(entities, expansion, boundaryResult.Boundaries);

        cancellationToken.ThrowIfCancellationRequested();

        var riskContext = new RiskAnalysisContext(entities, expansion.ExpandedGraph, boundaryResult, expansion, validation);

        onStageStarting?.Invoke(PipelineStage.RiskAnalysis);
        var riskResult = new RiskRuleEngine(RiskRules).Analyze(riskContext);
        var aggregation = new RiskAggregator().Aggregate(riskContext, riskResult);

        onStageStarting?.Invoke(PipelineStage.MigrationAssessment);
        var assessment = new MigrationAssessmentEngine().Assess(riskContext, riskResult, aggregation);
        var plan = MigrationPlanEngine.Plan(assessment);

        onStageStarting?.Invoke(PipelineStage.Reporting);
        var report = ServerMigrationAssessmentReportEngine.Build(riskContext, aggregation, assessment, plan, discovery);

        // GUI-6A: carry the already-computed discovery snapshot, application boundaries, and
        // external dependencies through unchanged — same instances the stages above already
        // built, never recomputed — so a presentation layer can show them without re-running
        // discovery or correlation itself.
        return new ScanPipelineResult
        {
            Aggregation = aggregation,
            Report = report,
            Discovery = discovery,
            Boundaries = boundaryResult.Boundaries,
            ExternalDependencies = expansion.ExternalDependencies
        };
    }
}
