namespace ServerSleuth.Analysis.Migration.Preparation;

/// <summary>
/// GUI-9B, relocated to Analysis by GUI-10 (see <see cref="MigrationIntent"/>'s doc comment for
/// why): the single source of truth for "which <see cref="MigrationIntent"/>(s) apply to this
/// discovered category." Replaces the category vocabulary previously duplicated across
/// <c>HtmlReportRenderer.AppendMigrationChecklist</c>, <c>ApplicationDetailView.xaml</c>'s
/// per-section <c>AppDetail.Action.*</c> labels, and <c>LocalizedStrings</c> — this catalog is
/// the business-data-model half of that vocabulary (which intents apply to which category);
/// localized display text remains a presentation concern of its own callers and is not part of
/// this model (skill.md GUI-9B §4).
///
/// Category keys match the exact <c>InventoryEntityDto.EntityType</c> discriminator strings
/// <c>ReportDtoMapper</c> already assigns (<c>"Dll"</c>, <c>"Runtime"</c>, <c>"Service"</c>,
/// <c>"ComComponent"</c>, <c>"Software"</c>, <c>"ScheduledTask"</c>, <c>"Certificate"</c>,
/// <c>"Configuration"</c>, <c>"ExternalDependency"</c>) — the same discriminators the GUI's own
/// <c>DiscoveryEntity.Type</c>/component view models already use for these categories — plus
/// <see cref="ApplicationCategory"/> for the report's <c>Applications</c> collection / the GUI's
/// own application-boundary count (neither of which is an <c>InventoryEntityDto</c>).
///
/// Deliberately reads no Risk/Migration-assessment state — this is a static, deterministic
/// lookup table only (skill.md GUI-9B §1, §11; GUI-10 §4, §11, §12).
/// </summary>
public static class MigrationIntentCatalog
{
    /// <summary>Category key for the Applications collection — not an
    /// <c>InventoryEntityDto.EntityType</c> value, kept here so the full "what must happen to
    /// every discovered thing" picture (skill.md GUI-9B §3) includes the application itself, not
    /// only its components.</summary>
    public const string ApplicationCategory = "Application";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<MigrationIntent>> IntentsByCategory =
        new Dictionary<string, IReadOnlyList<MigrationIntent>>(StringComparer.Ordinal)
        {
            [ApplicationCategory] = [MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify],
            ["Dll"] = [MigrationIntent.Deploy, MigrationIntent.Verify],
            ["Runtime"] = [MigrationIntent.Install, MigrationIntent.Verify],
            ["Service"] = [MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify],
            ["ComComponent"] = [MigrationIntent.Register, MigrationIntent.Verify],
            ["Software"] = [MigrationIntent.Install, MigrationIntent.Verify],
            ["ScheduledTask"] = [MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify],
            ["Certificate"] = [MigrationIntent.Install, MigrationIntent.Verify],
            ["Configuration"] = [MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify],
            ["ExternalDependency"] = [MigrationIntent.Configure, MigrationIntent.Verify]
        };

    /// <summary>Every category key this catalog knows about, in the fixed declaration order
    /// above — used by <see cref="MigrationPreparationSummaryBuilder"/> so the same category
    /// order is walked deterministically regardless of dictionary enumeration order.</summary>
    public static IReadOnlyList<string> Categories { get; } = IntentsByCategory.Keys.ToList();

    /// <summary>The intents for <paramref name="category"/>, or an empty list for an unknown
    /// category — never guessed, never a default/fallback intent (skill.md GUI-9B §3).</summary>
    public static IReadOnlyList<MigrationIntent> IntentsFor(string category) =>
        IntentsByCategory.TryGetValue(category, out var intents) ? intents : [];
}
