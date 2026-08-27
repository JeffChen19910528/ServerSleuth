using ServerSleuth.Gui.Composition;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>
/// GUI-1 §Step10: the GUI is presentation/application orchestration only — it must never grow a
/// type that duplicates (even partially) one of the existing pipeline engines it is meant to
/// eventually CONSUME. Verified by reflecting the whole compiled <c>ServerSleuth.Gui</c>
/// assembly for any type NAME matching one of the real engines.
/// </summary>
public class NoDuplicatePipelineEngineTests
{
    private static readonly string[] ForbiddenTypeNames =
    [
        "DiscoveryEngine", "CorrelationEngine", "ApplicationBoundaryEngine", "DependencyExpansionEngine",
        "GraphValidator", "RiskRuleEngine", "RiskAggregator", "MigrationAssessmentEngine",
        "MigrationPlanEngine", "ServerMigrationAssessmentReportEngine", "ScanPipelineRunner", "ReportDtoMapper"
    ];

    [Fact]
    public void GuiAssembly_DefinesNoTypeNamedAfterAnExistingPipelineEngine()
    {
        var assembly = typeof(CompositionRoot).Assembly;
        var offenders = assembly.GetTypes().Where(t => ForbiddenTypeNames.Contains(t.Name)).ToList();

        Assert.Empty(offenders);
    }
}
