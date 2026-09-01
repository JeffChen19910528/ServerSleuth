using System.Reflection;

namespace ServerSleuth.Gui.ExecutionHost.Tests;

/// <summary>
/// GUI-5 §7: "Export does not invoke DiscoveryEngine / ScanPipelineRunner." Unlike
/// <c>ServerSleuth.Gui</c>'s own ViewModels, this class IS allowed to reference
/// <c>ReportArtifactFactory</c>/<c>IReportExporter</c> — that is its entire job (see its own doc
/// comment) — so the forbidden list here is deliberately narrower than
/// <c>NoScanExecutionFromGuiTests</c>' one: everything upstream of an already-complete
/// <see cref="ServerSleuth.Analysis.Migration.Consolidation.ServerMigrationAssessmentReport"/>.
/// </summary>
public class GuiReportExportServiceArchitectureTests
{
    private static readonly string[] ForbiddenTypeNamePatterns =
    [
        "DiscoveryEngine", "ScanPipelineRunner", "DiscoveryScannerRegistry", "RiskRuleEngine", "MigrationAssessmentEngine",
        "RiskAggregator", "MigrationPlanEngine"
    ];

    [Fact]
    public void GuiReportExportService_HasNoFieldOfAnOrchestrationOrDiscoveryType()
    {
        var fields = typeof(GuiReportExportService).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var field in fields)
        {
            var name = field.FieldType.Name;
            Assert.DoesNotContain(ForbiddenTypeNamePatterns, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void GuiReportExportService_HasNoMethodReturningOrAcceptingAnOrchestrationOrDiscoveryType()
    {
        var methods = typeof(GuiReportExportService).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var involvedTypeNames = method.GetParameters().Select(p => p.ParameterType.Name).Append(method.ReturnType.Name);
            foreach (var name in involvedTypeNames)
            {
                Assert.DoesNotContain(ForbiddenTypeNamePatterns, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
            }
        }
    }

    /// <summary>The one method that actually performs the export takes exactly a completed pipeline
    /// result (never an engine or live runner), an output directory string, and the GUI's own
    /// format/policy enums — never anything from the discovery/correlation/risk/migration ENGINE
    /// side of the pipeline. GUI-8C: parameter upgraded from ServerMigrationAssessmentReport to
    /// ScanPipelineResult so inventory data reaches the HTML renderer.</summary>
    [Fact]
    public void Export_MethodSignature_TakesOnlyAnAlreadyCompleteReport_NeverAnEngine()
    {
        var method = typeof(GuiReportExportService).GetMethod(nameof(GuiReportExportService.Export));
        Assert.NotNull(method);

        var parameterTypeNames = method!.GetParameters().Select(p => p.ParameterType.Name).ToList();
        Assert.Contains("ScanPipelineResult", parameterTypeNames);
        Assert.DoesNotContain(parameterTypeNames, name => ForbiddenTypeNamePatterns.Any(f => name.Contains(f, StringComparison.Ordinal)));
    }
}
