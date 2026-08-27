using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.ViewModels;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>GUI-1 §5, §12 / GUI-2 §Step8: no credential-shaped property anywhere in PERSISTENT
/// GUI state or the ViewModels bound to it. Deliberately does NOT include "hostfingerprint" in
/// the forbidden-substring list — skill.md GUI-2's own explicit "do not over-match legitimate
/// fields" instruction: an SSH host-key fingerprint is a PUBLIC value (the whole point of
/// fingerprint-based trust), not a secret, so <c>ScanConfigurationState.SshHostFingerprint</c>/
/// <c>ScanConfigurationViewModel.SshHostFingerprint</c> are legitimate, intended properties, not
/// credential leaks. <see cref="ScanCredentialInput"/> is deliberately EXCLUDED from this list —
/// it is the one place <c>Username</c>/<c>Password</c> are SUPPOSED to exist (see its own
/// dedicated tests instead).</summary>
public class NoCredentialShapedGuiStateTests
{
    // "privatekey"/"passphrase" are deliberately NOT here: ScanConfigurationState legitimately
    // has SshPrivateKeyPath (a file PATH, never the key bytes) and
    // SshPrivateKeyPassphraseEnvironmentVariable (an environment-variable NAME, never the
    // passphrase VALUE it names) — the same "public metadata, not secret material" reasoning
    // already established for SshHostFingerprint. See
    // <see cref="ScanConfigurationState_SshMetadataProperties_AreStringPathsOrNames_NeverRawKeyMaterial"/>
    // for the TYPE-SHAPE check that actually protects against a future raw-secret field.
    private static readonly string[] ForbiddenSubstrings =
    [
        "password", "credential", "apikey", "bearertoken", "secretvalue", "username"
    ];

    private static readonly Type[] StateAndViewModelTypes =
    [
        typeof(GuiApplicationState), typeof(MainViewModel), typeof(NavigationItemViewModel), typeof(PlaceholderPageViewModel),
        typeof(ScanConfigurationState), typeof(ScanRequest),
        // GUI-3 §Step11, §Step15: none of the execution-facing types may carry a
        // credential-shaped property either — progress/completion/state are all read
        // (and, for ScanExecutionState, bound to the UI) long after any credential handling
        // has finished.
        typeof(ScanExecutionState), typeof(ScanProgressState), typeof(ScanCompletionState), typeof(ScannerProgressInfo), typeof(ScanExecutionViewModel),
        // GUI-4 §Step20: the Results Dashboard reads a completed scan's full pipeline result —
        // none of its own types may add a credential-shaped property either, even though they
        // now sit "closer" to Analysis/report data than any prior GUI-3 type did.
        typeof(ResultsDashboardViewModel), typeof(ApplicationRowViewModel), typeof(ApplicationDetailViewModel),
        // GUI-5 §10: every new Export/Report Viewer state/model type gets the same sweep.
        typeof(GuiReportExportResult), typeof(GuiReportViewResult)
    ];

    [Theory]
    [MemberData(nameof(TypesData))]
    public void Type_HasNoCredentialShapedPropertyName(Type type)
    {
        var offenders = type.GetProperties()
            .Select(p => p.Name)
            .Where(name => ForbiddenSubstrings.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    public static IEnumerable<object[]> TypesData() => StateAndViewModelTypes.Select(t => new object[] { t });

    [Fact]
    public void GuiApplicationState_TargetProperty_IsTheCredentialFreeScanTargetType_NeverAWiderObjectType()
    {
        var targetProperty = typeof(GuiApplicationState).GetProperty(nameof(GuiApplicationState.Target));
        Assert.NotNull(targetProperty);
        Assert.Equal(typeof(ServerSleuth.Core.Targets.ScanTarget), targetProperty!.PropertyType);
    }

    /// <summary>GUI-2: <see cref="ScanConfigurationViewModel"/> legitimately has a bindable
    /// <c>Username</c> property (a username alone is not secret, and the UI needs a TextBox to
    /// bind to) — but must NEVER have a <c>Password</c> PROPERTY of any kind; the password only
    /// ever exists as a method parameter (<c>SetPassword(SecureString)</c>), never a bound,
    /// gettable value.</summary>
    [Fact]
    public void ScanConfigurationViewModel_HasNoPasswordProperty_OnlyAWriteOnlyMethod()
    {
        var propertyNames = typeof(ScanConfigurationViewModel).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain(propertyNames, name => name.Contains("password", StringComparison.Ordinal));

        var setPassword = typeof(ScanConfigurationViewModel).GetMethod(nameof(ScanConfigurationViewModel.SetPassword));
        Assert.NotNull(setPassword);
        Assert.Equal(typeof(void), setPassword!.ReturnType);
        Assert.Equal(typeof(System.Security.SecureString), Assert.Single(setPassword.GetParameters()).ParameterType);
    }

    /// <summary>The actual security property behind the "privatekey"/"passphrase" exclusion
    /// above: both fields must be plain <see cref="string"/> (a path, a variable name) — never
    /// <see cref="byte"/>[]/<see cref="System.Security.SecureString"/>/any type shaped to hold
    /// real key or passphrase material.</summary>
    [Fact]
    public void ScanConfigurationState_SshMetadataProperties_AreStringPathsOrNames_NeverRawKeyMaterial()
    {
        var pathProperty = typeof(ScanConfigurationState).GetProperty(nameof(ScanConfigurationState.SshPrivateKeyPath));
        var passphraseEnvProperty = typeof(ScanConfigurationState).GetProperty(nameof(ScanConfigurationState.SshPrivateKeyPassphraseEnvironmentVariable));

        Assert.Equal(typeof(string), pathProperty!.PropertyType);
        Assert.Equal(typeof(string), passphraseEnvProperty!.PropertyType);
    }

    /// <summary>The one place <c>Username</c>/<c>Password</c> are SUPPOSED to exist —
    /// <see cref="ScanCredentialInput.Password"/> must be a <see cref="System.Security.SecureString"/>,
    /// never a plain <see cref="string"/>.</summary>
    [Fact]
    public void ScanCredentialInput_PasswordProperty_IsSecureString_NeverAPlainString()
    {
        var passwordProperty = typeof(ScanCredentialInput).GetProperty(nameof(ScanCredentialInput.Password));
        Assert.NotNull(passwordProperty);
        Assert.Equal(typeof(System.Security.SecureString), passwordProperty!.PropertyType);
    }
}
