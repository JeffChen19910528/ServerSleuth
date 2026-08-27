using System.Reflection;
using ServerSleuth.Analysis.Correlation;

namespace ServerSleuth.Analysis.Tests.Architecture;

/// <summary>
/// Phase 10C §13: Correlation/Boundary/Expansion/Validation/Risk/Migration/Reporting must never
/// need to know whether discovery was local or remote — they operate purely on already-produced
/// <c>DiscoveryEntity</c>/<c>DependencyGraph</c>/<c>ApplicationBoundary</c>/finding data. This is
/// verified structurally (reflection over the whole compiled assembly), not just by convention:
/// no public type in <c>ServerSleuth.Analysis</c> may have a public method parameter or property
/// of type <c>ScanTarget</c>/<c>ITargetTransport</c>/<c>DiscoveryContext</c> — if one ever did,
/// Analysis would have started depending on a Discovery/CLI-layer concern it has no business
/// knowing about.
///
/// Phase GUI-3 §Step1/§Step5: <c>ServerSleuth.Analysis.Orchestration.ScanPipelineRunner</c> is a
/// deliberate, narrow exception — it moved here from <c>ServerSleuth.Cli.Pipeline</c> so the CLI
/// and the GUI execution host can share the exact same orchestration code (see its own doc
/// comment), and its one job IS to bridge Discovery and Analysis, so its
/// <c>DiscoverAsync(DiscoveryContext, ...)</c> method legitimately takes a
/// <see cref="ServerSleuth.Core.Interfaces.DiscoveryContext"/> — it is not one of the pure,
/// target-agnostic computation engines (Correlation/Boundary/Expansion/Validation/Risk/
/// Migration/Reporting) this test protects. Every one of those engines remains completely
/// unrepresented in <see cref="ExemptTypeNames"/> below — this exemption is scoped to exactly
/// one type, not a general carve-out.
/// </summary>
public class TargetAgnosticismTests
{
    private static readonly string[] ForbiddenTypeNames =
    [
        "ServerSleuth.Core.Targets.ScanTarget",
        "ServerSleuth.Infrastructure.Targets.ITargetTransport",
        "ServerSleuth.Core.Interfaces.DiscoveryContext"
    ];

    private static readonly string[] ExemptTypeNames =
    [
        "ServerSleuth.Analysis.Orchestration.ScanPipelineRunner"
    ];

    [Fact]
    public void NoPublicMethodParameter_InAnalysisAssembly_MentionsATargetOrDiscoveryContextType()
    {
        var assembly = typeof(CorrelationEngine).Assembly;
        var offenders = new List<string>();

        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic && !ExemptTypeNames.Contains(t.FullName)))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                foreach (var parameter in method.GetParameters())
                {
                    if (ForbiddenTypeNames.Contains(parameter.ParameterType.FullName))
                    {
                        offenders.Add($"{type.FullName}.{method.Name}({parameter.ParameterType.FullName} {parameter.Name})");
                    }
                }

                if (ForbiddenTypeNames.Contains(method.ReturnType.FullName))
                {
                    offenders.Add($"{type.FullName}.{method.Name}() returns {method.ReturnType.FullName}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoPublicProperty_InAnalysisAssembly_MentionsATargetOrDiscoveryContextType()
    {
        var assembly = typeof(CorrelationEngine).Assembly;
        var offenders = new List<string>();

        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (ForbiddenTypeNames.Contains(property.PropertyType.FullName))
                {
                    offenders.Add($"{type.FullName}.{property.Name} : {property.PropertyType.FullName}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void ServerSleuthAnalysis_HasNoProjectReferenceToServerSleuthInfrastructure()
    {
        // Analysis operating on entities/graphs/findings alone (never a transport/target
        // concern) is enforced structurally: it cannot even compile against
        // ServerSleuth.Infrastructure.Targets.ITargetTransport unless it references that
        // assembly, and it does not.
        var referencedAssemblyNames = typeof(CorrelationEngine).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("ServerSleuth.Infrastructure", referencedAssemblyNames);
    }
}
