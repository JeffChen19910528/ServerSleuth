using System.Reflection;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>
/// GUI-2 §Step9-10 / GUI-3 §Step2: "do not instantiate DiscoveryEngine or ScanPipelineRunner
/// from a ViewModel." <c>ServerSleuth.Core.Orchestration.IDiscoveryEngine</c>/
/// <c>ServerSleuth.Analysis.Orchestration.ScanPipelineRunner</c> are technically REACHABLE from
/// <c>ServerSleuth.Gui</c> (both live in allowed dependencies), so the guarantee here is about
/// USAGE, not mere referenceability — this test proves neither
/// <see cref="ScanConfigurationViewModel"/> nor <see cref="ScanExecutionViewModel"/> ever takes
/// an orchestration-shaped dependency at all (constructor parameters, fields, method
/// signatures) — <see cref="ScanExecutionViewModel"/>'s ONLY dependency is the abstract
/// <c>IGuiScanExecutor</c>, meaning it structurally CANNOT call into a real pipeline/scanner/
/// transport even though the types are technically visible to the assembly.
/// </summary>
public class NoScanExecutionFromGuiTests
{
    private static readonly string[] ForbiddenTypeNamePatterns =
    [
        "DiscoveryEngine", "ScanPipelineRunner", "DiscoveryScannerRegistry", "RiskRuleEngine", "MigrationAssessmentEngine", "ReportArtifactFactory",
        // GUI-4 §Step21: the two additional engine types that specific phase explicitly calls out.
        "RiskAggregator", "MigrationPlanEngine"
    ];

    private static readonly Type[] CheckedViewModelTypes =
    [
        typeof(ScanConfigurationViewModel), typeof(ScanExecutionViewModel),
        // GUI-4 §Step21: ResultsDashboardViewModel must be built purely from an already-completed
        // ScanExecutionState — never by constructing any orchestration/engine type itself.
        typeof(ResultsDashboardViewModel), typeof(ApplicationDetailViewModel), typeof(ApplicationRowViewModel),
        // GUI-6 §2: the shell ViewModel that wires the whole workflow together (Scan Configuration
        // → Execution → Results → New Scan) gets the same sweep — it must remain pure navigation
        // glue, never a place an orchestration/engine dependency could sneak in.
        typeof(MainViewModel)
    ];

    public static IEnumerable<object[]> ViewModelTypes() => CheckedViewModelTypes.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(ViewModelTypes))]
    public void ViewModel_ConstructorTakesNoOrchestrationOrExecutionDependency(Type viewModelType)
    {
        var constructor = Assert.Single(viewModelType.GetConstructors());
        var parameterTypeNames = constructor.GetParameters().Select(p => p.ParameterType.Name);

        foreach (var name in parameterTypeNames)
        {
            Assert.DoesNotContain(ForbiddenTypeNamePatterns, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(ViewModelTypes))]
    public void ViewModel_HasNoFieldOfAnOrchestrationOrExecutionType(Type viewModelType)
    {
        var fields = viewModelType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var field in fields)
        {
            var name = field.FieldType.Name;
            Assert.DoesNotContain(ForbiddenTypeNamePatterns, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(ViewModelTypes))]
    public void ViewModel_HasNoMethodReturningOrAcceptingAnOrchestrationType(Type viewModelType)
    {
        var methods = viewModelType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var involvedTypeNames = method.GetParameters().Select(p => p.ParameterType.Name).Append(method.ReturnType.Name);
            foreach (var name in involvedTypeNames)
            {
                Assert.DoesNotContain(ForbiddenTypeNamePatterns, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
            }
        }
    }

    /// <summary>GUI-3 §Step2: <see cref="ScanExecutionViewModel"/>'s ONLY constructor dependency
    /// is the abstract <c>IGuiScanExecutor</c> — no <c>IApplicationStateService</c> either (the
    /// same "cannot even write into persistent state" guarantee GUI-2 already established for
    /// <see cref="ScanConfigurationViewModel"/>).</summary>
    [Fact]
    public void ScanExecutionViewModel_HasExactlyOneConstructorDependency_TheAbstractExecutor()
    {
        var constructor = Assert.Single(typeof(ScanExecutionViewModel).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal("IGuiScanExecutor", parameter.ParameterType.Name);
    }
}
