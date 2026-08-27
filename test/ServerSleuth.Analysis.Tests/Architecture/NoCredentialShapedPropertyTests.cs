using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Tests.Architecture;

/// <summary>
/// Phase 10E-3 §G: structural proof that no Risk/Migration/Report-consolidation model has a
/// dedicated credential-shaped property — narrower and more specific than the existing
/// <c>TargetAgnosticismTests</c> (which proves these types never reference <c>ScanTarget</c>/
/// <c>ITargetTransport</c> at all) and the existing report-content string sweeps (Phase 10E-1/
/// 10E-2, which scan rendered JSON/HTML text for injected sentinel secret VALUES). This test
/// instead inspects the TYPE SHAPE itself — proving there is nowhere on these models a
/// credential could even be assigned, independent of what any particular scan run happens to
/// discover.
/// </summary>
public class NoCredentialShapedPropertyTests
{
    private static readonly Type[] ModelTypes =
    [
        typeof(RiskFinding),
        typeof(MigrationIssue),
        typeof(MigrationAction),
        typeof(MigrationVerificationCheck),
        typeof(ServerMigrationAssessmentReport)
    ];

    private static readonly string[] ForbiddenSubstrings =
    [
        "password", "credential", "privatekey", "passphrase", "apikey", "bearertoken", "secretvalue"
    ];

    [Theory]
    [MemberData(nameof(ModelTypesData))]
    public void ModelType_HasNoCredentialShapedPropertyName(Type type)
    {
        var offenders = type.GetProperties()
            .Select(p => p.Name)
            .Where(name => ForbiddenSubstrings.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    public static IEnumerable<object[]> ModelTypesData() => ModelTypes.Select(t => new object[] { t });

    [Fact]
    public void RiskFinding_MetadataValues_AreStringOnly_NeverAStronglyTypedCredentialObject()
    {
        // Metadata is a free-form Dictionary<string,string> — its VALUES are covered by the
        // existing ISecretRedactor-backed content sweeps elsewhere; what this test proves is
        // that the *type* itself cannot hold anything other than a string in that slot (i.e. no
        // future change could widen it to `object?` and accidentally accept a credential type).
        var metadataProperty = typeof(RiskFinding).GetProperty(nameof(RiskFinding.Metadata));
        Assert.NotNull(metadataProperty);
        Assert.Equal(typeof(IReadOnlyDictionary<string, string>), metadataProperty!.PropertyType);
    }
}
