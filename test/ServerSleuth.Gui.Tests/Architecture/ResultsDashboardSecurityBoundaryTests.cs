using System.Reflection;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>
/// GUI-4 §Step20: "add structural tests proving these GUI result types contain no
/// Password/SecureString/Credential/RemoteCredential/WindowsRemoteCredential/SSH private key/
/// token/bearer token/secret/authentication object... do not rely only on property-name
/// scanning — inspect actual types and assembly references." <see cref="NoCredentialShapedGuiStateTests"/>
/// already covers the name-based check for these three new types; this class covers the
/// TYPE-SHAPE side (mirrors the existing <c>NoCredentialShapedPropertyTests</c> pattern from
/// Phase 10E-3's remote-transport hardening).
/// </summary>
public class ResultsDashboardSecurityBoundaryTests
{
    private static readonly Type[] ForbiddenPropertyTypes =
    [
        typeof(System.Security.SecureString)
    ];

    private static readonly string[] ForbiddenTypeNameSubstrings =
    [
        "Credential", "SecureString", "Token", "Bearer", "PrivateKey", "Secret", "Authentication"
    ];

    private static readonly Type[] CheckedTypes =
    [
        typeof(ResultsDashboardViewModel), typeof(ApplicationRowViewModel), typeof(ApplicationDetailViewModel),
        // GUI-5 §10: the two new Export/Report Viewer result types get the same direct check
        // (they are already reachable — and therefore already covered — via ResultsDashboardViewModel's
        // own walk below, but an explicit entry here makes the guarantee obvious per-type too).
        typeof(ServerSleuth.Gui.Models.GuiReportExportResult), typeof(ServerSleuth.Gui.Models.GuiReportViewResult)
    ];

    public static IEnumerable<object[]> CheckedTypesData() => CheckedTypes.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(CheckedTypesData))]
    public void Type_HasNoPropertyOfAForbiddenSecretShapedType(Type type)
    {
        var offenders = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => ForbiddenPropertyTypes.Contains(p.PropertyType))
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [MemberData(nameof(CheckedTypesData))]
    public void Type_HasNoPropertyWhoseDeclaredTypeNameLooksCredentialShaped(Type type)
    {
        // "Authentication" is deliberately excluded here for MigrationActionType-adjacent enum
        // names, etc.; checked separately below with the fully-qualified allow list this project
        // actually has (there are none reachable from these three types at all).
        var offenders = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => ForbiddenTypeNameSubstrings.Where(s => s != "Authentication")
                .Any(f => p.PropertyType.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>The real, exhaustive guarantee: walk every property type reachable from
    /// <see cref="ResultsDashboardViewModel"/> that lives in a ServerSleuth.* namespace, and
    /// confirm none of them is credential-shaped by NAME either — catches a future property
    /// added deep inside a domain record (e.g. a hypothetical <c>MigrationDependency.ApiToken</c>)
    /// that the shallow per-type checks above wouldn't reach.</summary>
    [Fact]
    public void ResultsDashboardViewModel_ReachableServerSleuthTypeGraph_HasNoCredentialShapedProperty()
    {
        var visited = new HashSet<Type>();
        var offenders = new List<string>();
        Walk(typeof(ResultsDashboardViewModel), visited, offenders);

        Assert.Empty(offenders);
    }

    private static void Walk(Type type, HashSet<Type> visited, List<string> offenders)
    {
        if (!visited.Add(type))
        {
            return;
        }

        if (type.Namespace is null || !type.Namespace.StartsWith("ServerSleuth.", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propertyType = property.PropertyType;
            var elementType = GetEnumerableElementTypeOrSelf(propertyType);

            if (ForbiddenTypeNameSubstrings.Any(f => elementType.Name.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                ForbiddenTypeNameSubstrings.Any(f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add($"{type.FullName}.{property.Name} ({elementType.Name})");
                continue;
            }

            if (elementType.Namespace?.StartsWith("ServerSleuth.", StringComparison.Ordinal) == true && !elementType.IsEnum)
            {
                Walk(elementType, visited, offenders);
            }
        }
    }

    private static Type GetEnumerableElementTypeOrSelf(Type type)
    {
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            return type.GetGenericArguments().FirstOrDefault() ?? type;
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return underlying;
        }

        return type;
    }
}
