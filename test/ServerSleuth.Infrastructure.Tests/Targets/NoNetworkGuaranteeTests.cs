using System.Reflection;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Tests.Targets;

/// <summary>
/// Phase 10D-1 §20-21: this phase (10D-1 itself) made ZERO network connections — no socket, no
/// HTTP call, no SSH connection, no WinRM connection. Verified structurally: none of the Phase
/// 10D-1 types reference a networking BCL namespace. Phase 10D-2 DELIBERATELY adds exactly one
/// new package (SSH.NET) to actually implement the Linux/SSH transport — see
/// <c>InfrastructureProject_AddsOnlyTheApprovedSshPackage_ForPhase10D2</c> below, which replaces
/// the old "zero new packages" assertion with an explicit allow-list of exactly what Phase
/// 10D-2 was authorized to add. No WinRM/PowerShell-remoting package was added.
/// </summary>
public class NoNetworkGuaranteeTests
{
    private static readonly Type[] Phase10D1Types =
    [
        typeof(RemoteOperation),
        typeof(RemoteOperationResult),
        typeof(RemoteOperationKind),
        typeof(RemoteTransportKind),
        typeof(RemoteTargetTransportFactory)
    ];

    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "System.Net.Sockets",
        "System.Net.Http",
        "System.Net.WebSockets",
        "System.Net.Security"
    ];

    [Fact]
    public void Phase10D1Types_ReferenceNoNetworkingNamespace_InAnyPublicMemberSignature()
    {
        foreach (var type in Phase10D1Types)
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

    /// <summary>Phase 10D-2 §3: exactly one new package (SSH.NET) was added, and only to
    /// implement the Linux/SSH transport this phase's own objective requires — nothing
    /// WinRM/PowerShell-Remoting-shaped exists anywhere in the project file.</summary>
    [Fact]
    public void InfrastructureProject_AddsOnlyTheApprovedSshPackage_ForPhase10D2()
    {
        var csprojPath = FindCsproj();
        var content = File.ReadAllText(csprojPath);

        Assert.Contains("SSH.NET", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WinRM", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PowerShell", content, StringComparison.OrdinalIgnoreCase);

        var packageCount = content.Split("<PackageReference").Length - 1;
        Assert.Equal(3, packageCount); // the 2 pre-existing Microsoft.Extensions.* packages + SSH.NET
    }

    private static string FindCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ServerSleuth.Infrastructure", "ServerSleuth.Infrastructure.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate ServerSleuth.Infrastructure.csproj from the test output directory.");
    }
}
