namespace ServerSleuth.Analysis.Migration.Preparation;

/// <summary>One <see cref="MigrationIntent"/> and how many discovered items require it — see
/// <see cref="MigrationPreparationSummary"/>.</summary>
public sealed record MigrationIntentCount
{
    public required MigrationIntent Intent { get; init; }
    public required int Count { get; init; }
}

/// <summary>
/// GUI-9B, relocated to Analysis by GUI-10 (see <see cref="MigrationIntent"/>'s doc comment for
/// why): a computed, inventory-derived "what must be prepared on the destination server"
/// projection over per-category discovered-entity counts plus <see cref="MigrationIntentCatalog"/>
/// — never a second analysis pass, never a read of <c>MigrationIssue</c>/<c>MigrationAction</c>/
/// <c>RiskSeverity</c>/blocking-status (skill.md GUI-9B §1, §7, §11; GUI-10 §4, §11, §12).
///
/// <see cref="TotalInventoryCount"/> is the number of discovered entities counted across every
/// supplied category; <see cref="IntentCounts"/> is the number of required preparation intents —
/// deliberately different numbers, since one entity legitimately contributes to multiple intents
/// (e.g. a Windows Service is Create + Configure + Verify all at once — skill.md GUI-9B §8,
/// GUI-10 §5). Do not read one as a substitute for the other.
/// </summary>
public sealed record MigrationPreparationSummary
{
    public required int TotalInventoryCount { get; init; }

    /// <summary>Always exactly one entry per <see cref="MigrationIntent"/> value, in enum
    /// declaration order (Deploy, Install, Create, Register, Configure, Verify, Review) —
    /// deterministic regardless of which categories are present. A count of zero (e.g. Review,
    /// which no current category maps to) is included explicitly rather than omitted, so the
    /// absence of a real Review source is visible, not silently missing (skill.md GUI-9B §7).</summary>
    public required IReadOnlyList<MigrationIntentCount> IntentCounts { get; init; }

    /// <summary>The all-zero summary — used both by Reporting (a report built without discovery
    /// data) and by the GUI (no completed scan yet) for the same reason the nine inventory
    /// lists/entity counts default to empty/zero elsewhere.</summary>
    public static MigrationPreparationSummary Empty { get; } = new()
    {
        TotalInventoryCount = 0,
        IntentCounts = Enum.GetValues<MigrationIntent>()
            .Select(intent => new MigrationIntentCount { Intent = intent, Count = 0 })
            .ToList()
    };
}

/// <summary>Builds <see cref="MigrationPreparationSummary"/> from already-computed per-category
/// discovered-entity counts — pure, side-effect-free, reads only integers the caller already
/// computed (never item content, never Risk/Assessment fields), so it cannot depend on Risk by
/// construction (skill.md GUI-9B §1, §7, §11; GUI-10 §4, §11, §12). Deliberately takes plain
/// (category, count) pairs rather than any Reporting- or GUI-specific DTO/view-model type, so
/// both <c>ServerSleuth.Reporting</c>'s <c>ReportDtoMapper</c> and <c>ServerSleuth.Gui</c>'s
/// Migration page can call the exact same builder against their own already-computed counts
/// without either depending on the other's presentation types.</summary>
public static class MigrationPreparationSummaryBuilder
{
    /// <summary>
    /// <paramref name="countsByCategory"/> keys should be <see cref="MigrationIntentCatalog"/>
    /// category strings (<see cref="MigrationIntentCatalog.Categories"/>) — an unrecognized key
    /// contributes to <see cref="MigrationPreparationSummary.TotalInventoryCount"/> but to no
    /// intent (<see cref="MigrationIntentCatalog.IntentsFor"/> returns empty for it), consistent
    /// with "never guess an intent for an unknown category."
    /// </summary>
    public static MigrationPreparationSummary Build(IEnumerable<(string Category, int Count)> countsByCategory)
    {
        var countsByIntent = Enum.GetValues<MigrationIntent>().ToDictionary(intent => intent, _ => 0);
        var total = 0;

        foreach (var (category, count) in countsByCategory)
        {
            total += count;
            foreach (var intent in MigrationIntentCatalog.IntentsFor(category))
            {
                countsByIntent[intent] += count;
            }
        }

        return new MigrationPreparationSummary
        {
            TotalInventoryCount = total,
            IntentCounts = Enum.GetValues<MigrationIntent>()
                .Select(intent => new MigrationIntentCount { Intent = intent, Count = countsByIntent[intent] })
                .ToList()
        };
    }
}
