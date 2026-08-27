using System.Reflection;
using ServerSleuth.Windows.Remote;

namespace ServerSleuth.Windows.Tests.Remote;

/// <summary>
/// Phase 10D-3A §20: structural architecture guarantees for the Windows remote capability
/// model, verified by reflection rather than convention alone — the same style Phase 10C/
/// 10D-1/10D-2's own architecture tests already established.
/// </summary>
public class WindowsRemoteCapabilityArchitectureTests
{
    private static readonly Type[] CapabilityModelTypes =
    [
        typeof(IWindowsRemoteCapabilities),
        typeof(WindowsRemoteOperationKind),
        typeof(WindowsRemoteCapabilityFactory)
    ];

    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "ServerSleuth.Reporting",
        "ServerSleuth.Analysis",
        "System.Windows.Forms",
        "System.Windows.Controls",
        "Microsoft.AspNetCore"
    ];

    [Fact]
    public void CapabilityModelTypes_ReferenceNoGuiReportingOrAnalysisNamespace_InAnyPublicMemberSignature()
    {
        foreach (var type in CapabilityModelTypes)
        {
            var referencedTypes = type
                .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(DescribeReferencedTypes);

            foreach (var referenced in referencedTypes)
            {
                var ns = referenced.Namespace ?? string.Empty;
                Assert.DoesNotContain(ForbiddenNamespacePrefixes, forbidden => ns.StartsWith(forbidden, StringComparison.Ordinal));
            }
        }
    }

    private static IEnumerable<Type> DescribeReferencedTypes(MemberInfo member) => member switch
    {
        PropertyInfo p => [p.PropertyType],
        MethodInfo m => m.GetParameters().Select(par => par.ParameterType).Append(m.ReturnType),
        FieldInfo f => [f.FieldType],
        _ => []
    };

    [Fact]
    public void ServerSleuthWindowsProject_HasNoProjectReference_ToReportingOrAnalysis()
    {
        var csprojPath = FindCsproj("ServerSleuth.Windows");
        var content = File.ReadAllText(csprojPath);

        Assert.DoesNotContain("ServerSleuth.Reporting", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ServerSleuth.Analysis", content, StringComparison.Ordinal);
    }

    /// <summary>Phase 10D-3B §3, §30: exactly ONE new package was added —
    /// <c>Microsoft.Management.Infrastructure</c> (the same native CIM/WS-Man client library
    /// PowerShell's own CIM cmdlets use) — no PowerShell/WSMan-scripting/remoting package of any
    /// kind. See ARCHITECTURE.md's Phase 10D-3B addendum for the full package-selection
    /// rationale.</summary>
    [Fact]
    public void ServerSleuthWindowsProject_HasNoPowerShellOrRemotingScriptingPackage()
    {
        var csprojPath = FindCsproj("ServerSleuth.Windows");
        var content = File.ReadAllText(csprojPath);

        Assert.Contains("Microsoft.Management.Infrastructure", content, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Management.Automation", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PowerShell", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remoting", content, StringComparison.OrdinalIgnoreCase);

        // The five packages Phase 3/Phase 10D-3A's own inspection found, plus exactly one new
        // one (Microsoft.Management.Infrastructure) for Phase 10D-3B.
        var packageCount = content.Split("<PackageReference").Length - 1;
        Assert.Equal(6, packageCount);
    }

    /// <summary>Phase 10D-3A §20: "existing Linux SSH architecture remains unchanged" — the
    /// Infrastructure project (which hosts the Linux/SSH transport) gained no new package as
    /// part of THIS phase; SSH.NET is still the only one, unchanged from Phase 10D-2.</summary>
    [Fact]
    public void ServerSleuthInfrastructureProject_StillHasExactlyTheThreePhase10D2Packages_Unchanged()
    {
        var csprojPath = FindCsproj("ServerSleuth.Infrastructure");
        var content = File.ReadAllText(csprojPath);

        Assert.Contains("SSH.NET", content, StringComparison.OrdinalIgnoreCase);
        var packageCount = content.Split("<PackageReference").Length - 1;
        Assert.Equal(3, packageCount);
    }

    /// <summary>Phase 10D-3A §20: the Windows capability model must not leak into
    /// ServerSleuth.Infrastructure — it is a Windows-domain concept, exactly like
    /// IWindowsRegistryReader/IProcessWmiProvider already are.</summary>
    [Fact]
    public void ServerSleuthInfrastructureAssembly_DefinesNoWindowsRemoteCapabilityType()
    {
        var infrastructureAssembly = typeof(ServerSleuth.Infrastructure.Targets.ITargetTransport).Assembly;

        var offenders = infrastructureAssembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.StartsWith("WindowsRemote", StringComparison.Ordinal));

        Assert.Empty(offenders);
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
