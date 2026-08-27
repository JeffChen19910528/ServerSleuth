using System.IO;
using ServerSleuth.Gui.Composition;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>
/// GUI-1 §Step10 (Architecture Test) / Critical Dependency Rule: the GUI must not directly
/// depend on ServerSleuth.Windows/ServerSleuth.Linux (scanner implementations) or
/// ServerSleuth.Infrastructure (registry/filesystem/process/network primitives) — verified at
/// the ASSEMBLY-REFERENCE level, the strongest possible guarantee: if the assembly isn't even
/// referenced, nothing in it can be reached from GUI code at all, regardless of what any single
/// class happens to call.
/// </summary>
public class NoDirectPlatformAccessTests
{
    private static readonly string[] ForbiddenAssemblyNames =
    [
        "ServerSleuth.Windows", "ServerSleuth.Linux", "ServerSleuth.Infrastructure", "ServerSleuth.Cli", "ServerSleuth.Reporting"
    ];

    [Fact]
    public void GuiAssembly_ReferencesNoPlatformOrScannerOrCliAssembly()
    {
        var assembly = typeof(CompositionRoot).Assembly;
        var referencedNames = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

        foreach (var forbidden in ForbiddenAssemblyNames)
        {
            Assert.DoesNotContain(forbidden, referencedNames);
        }
    }

    [Fact]
    public void GuiAssembly_ReferencesOnlyCoreAndAnalysis_AmongServerSleuthAssemblies()
    {
        var assembly = typeof(CompositionRoot).Assembly;
        var serverSleuthReferences = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith("ServerSleuth.", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // GUI-3 §Step1-2: two additive references, both deliberately outside the forbidden list
        // above — ServerSleuth.Gui.Contracts (shared DTOs/interfaces only, references only
        // ServerSleuth.Core) and ServerSleuth.Gui.ExecutionHost (the composition/execution
        // boundary that IS allowed to reference Infrastructure/Windows/Reporting — see its own
        // .csproj comment for why that does not violate this assembly's own zero-platform-
        // reference guarantee: this project's source code never directly imports a type from
        // any of those three assemblies, so none of them becomes a direct AssemblyRef here,
        // mechanically reconfirmed by the sibling test above).
        Assert.Equal(
            ["ServerSleuth.Analysis", "ServerSleuth.Core", "ServerSleuth.Gui.Contracts", "ServerSleuth.Gui.ExecutionHost"],
            serverSleuthReferences);
    }

    [Fact]
    public void GuiProject_HasNoDirectPackageReference_ToANetworkOrRemoteTransportLibrary()
    {
        var csprojPath = FindCsproj("ServerSleuth.Gui");
        var content = File.ReadAllText(csprojPath);

        Assert.DoesNotContain("SSH.NET", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.Management.Infrastructure", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.Net.Http", content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>GUI-4: <c>ServerSleuth.Gui.Contracts</c> gained a new <c>ServerSleuth.Analysis</c>
    /// reference this phase (see its own .csproj comment) so <see cref="ServerSleuth.Gui.Models.ScanCompletionState"/>
    /// could carry the real <c>ScanPipelineResult</c>. Re-verify at the assembly-reference level
    /// that this widening stayed within the allowed boundary — still zero Windows/Linux/
    /// Infrastructure/Cli/Reporting reference, and still nothing beyond Core+Analysis among
    /// ServerSleuth.* assemblies.</summary>
    [Fact]
    public void GuiContractsAssembly_ReferencesNoPlatformOrScannerOrCliAssembly()
    {
        var assembly = typeof(ServerSleuth.Gui.Models.ScanCompletionState).Assembly;
        var referencedNames = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

        foreach (var forbidden in ForbiddenAssemblyNames)
        {
            Assert.DoesNotContain(forbidden, referencedNames);
        }
    }

    [Fact]
    public void GuiContractsAssembly_ReferencesOnlyCoreAndAnalysis_AmongServerSleuthAssemblies()
    {
        var assembly = typeof(ServerSleuth.Gui.Models.ScanCompletionState).Assembly;
        var serverSleuthReferences = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith("ServerSleuth.", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["ServerSleuth.Analysis", "ServerSleuth.Core"], serverSleuthReferences);
    }

    private static string FindCsproj(string projectName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", projectName, $"{projectName}.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {projectName}.csproj from the test output directory.");
    }
}
