using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Diagnostics;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Planning;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Gui.ExecutionHost.Tests.Fixtures;

/// <summary>GUI-5: a hand-built, minimal (zero-application) but fully valid
/// <see cref="ServerMigrationAssessmentReport"/> — <see cref="GuiReportExportServiceTests"/>
/// only needs SOMETHING real to hand <c>ReportArtifactFactory</c>/<c>LocalFileReportExporter</c>;
/// it is not exercising Correlation/Risk/Migration policy itself (that is
/// <c>ServerSleuth.Analysis.Tests</c>'/<c>ServerSleuth.Reporting.Tests</c>' own job).</summary>
internal static class MinimalReportFixture
{
    public static ServerMigrationAssessmentReport Build()
    {
        var serverAssessment = new ServerMigrationAssessment
        {
            OverallStatus = MigrationStatus.Ready,
            BlockingIssueCount = 0,
            RemediationIssueCount = 0,
            ConditionalDependencyCount = 0,
            InformationalIssueCount = 0,
            UnclassifiedIssueCount = 0,
            AffectedBoundaryCount = 0,
            AffectedEntityCount = 0,
            Issues = [],
            Dependencies = [],
            Evidence = [],
            ApplicationAssessments = []
        };

        var assessmentSummary = new MigrationAssessmentSummary { Server = serverAssessment, Diagnostics = new MigrationDiagnostics() };

        var plan = new MigrationPlan
        {
            Assessment = assessmentSummary,
            Actions = [],
            Dependencies = [],
            PreMigrationChecks = [],
            PostMigrationChecks = [],
            Diagnostics = new MigrationPlanDiagnostics { Actions = new MigrationActionDiagnostics(), Verification = new MigrationVerificationDiagnostics() }
        };

        var serverSummary = new ServerMigrationSummary
        {
            OverallMigrationStatus = MigrationStatus.Ready,
            OverallRiskSeverity = AggregateSeverity.None,
            ApplicationCount = 0,
            BlockedApplicationCount = 0,
            NeedsRemediationApplicationCount = 0,
            ReadyWithConditionsApplicationCount = 0,
            ReadyApplicationCount = 0,
            BlockingIssueCount = 0,
            RemediationIssueCount = 0,
            ConditionalDependencyCount = 0,
            ActionCount = 0,
            VerificationCheckCount = 0,
            DependencyCount = 0,
            AffectedEntityCount = 0,
            AffectedBoundaryCount = 0
        };

        return new ServerMigrationAssessmentReport
        {
            Assessment = assessmentSummary,
            Plan = plan,
            ServerSummary = serverSummary,
            ApplicationAssessments = [],
            ServerLevelIssues = [],
            SharedInfrastructure = [],
            Dependencies = [],
            Actions = [],
            PreMigrationChecks = [],
            PostMigrationChecks = [],
            Coverage = AssessmentCoverage.Complete,
            CoverageWarnings = [],
            GraphValidationErrors = [],
            Diagnostics = new ConsolidationDiagnostics()
        };
    }

    public static ScanPipelineResult BuildPipeline()
    {
        var server = new ServerRiskSummary
        {
            OverallSeverity = AggregateSeverity.None,
            CriticalCount = 0, HighCount = 0, MediumCount = 0, LowCount = 0, InfoCount = 0,
            TotalFindingCount = 0, AffectedEntityCount = 0, AffectedBoundaryCount = 0,
            Findings = [], TopRisks = [],
            CategoryCounts = new Dictionary<RiskCategory, int>(),
            SharedDependencyCount = 0,
            AggregateConfidence = new Confidence(0.0),
            ApplicationSummaries = [],
            ServerScopedFindingCount = 0
        };
        return new ScanPipelineResult
        {
            Report = Build(),
            Aggregation = new RiskAggregationResult { Server = server, Diagnostics = new RiskAggregationDiagnostics() }
        };
    }
}
