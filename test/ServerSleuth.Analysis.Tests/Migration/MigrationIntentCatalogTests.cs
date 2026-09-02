using ServerSleuth.Analysis.Migration.Preparation;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// GUI-9B §11, relocated here by GUI-10 alongside <c>MigrationIntentCatalog</c> itself (see its
/// doc comment for why: <c>ServerSleuth.Gui</c> may never reference <c>ServerSleuth.Reporting</c>,
/// so the catalog moved to Analysis for both sides to share) — proves the category →
/// <see cref="MigrationIntent"/> mapping is deterministic, closed, and reads no Risk/Assessment
/// state. This is a pure static-lookup test: no pipeline, no discovery, no report is built
/// anywhere in this file.
/// </summary>
public class MigrationIntentCatalogTests
{
    [Theory]
    [InlineData("Dll", new[] { MigrationIntent.Deploy, MigrationIntent.Verify })]
    [InlineData("Runtime", new[] { MigrationIntent.Install, MigrationIntent.Verify })]
    [InlineData("Service", new[] { MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify })]
    [InlineData("ComComponent", new[] { MigrationIntent.Register, MigrationIntent.Verify })]
    [InlineData("Software", new[] { MigrationIntent.Install, MigrationIntent.Verify })]
    [InlineData("ScheduledTask", new[] { MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify })]
    [InlineData("Certificate", new[] { MigrationIntent.Install, MigrationIntent.Verify })]
    [InlineData("Configuration", new[] { MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify })]
    [InlineData("ExternalDependency", new[] { MigrationIntent.Configure, MigrationIntent.Verify })]
    [InlineData(MigrationIntentCatalog.ApplicationCategory, new[] { MigrationIntent.Create, MigrationIntent.Configure, MigrationIntent.Verify })]
    public void IntentsFor_ReturnsTheApprovedMapping(string category, MigrationIntent[] expected)
    {
        Assert.Equal(expected, MigrationIntentCatalog.IntentsFor(category));
    }

    [Fact]
    public void IntentsFor_UnknownCategory_ReturnsEmpty_NeverGuessed()
    {
        Assert.Empty(MigrationIntentCatalog.IntentsFor("SomethingNeverDiscovered"));
    }

    [Fact]
    public void IntentsFor_IsDeterministic_AcrossRepeatedCalls()
    {
        var first = MigrationIntentCatalog.IntentsFor("Service");
        var second = MigrationIntentCatalog.IntentsFor("Service");
        Assert.Equal(first, second);
    }

    [Fact]
    public void NoCategory_HasDuplicateIntents()
    {
        foreach (var category in MigrationIntentCatalog.Categories)
        {
            var intents = MigrationIntentCatalog.IntentsFor(category);
            Assert.Equal(intents.Distinct(), intents);
        }
    }

    [Fact]
    public void NoCategory_MapsToReview_UnderTheCurrentApprovedMapping()
    {
        // GUI-9B §7: "do not force Review into every category" / "if there is no real Review
        // source, the count may simply be zero" — the currently approved mapping has no category
        // that legitimately requires Review, so no category should produce it today.
        foreach (var category in MigrationIntentCatalog.Categories)
        {
            Assert.DoesNotContain(MigrationIntent.Review, MigrationIntentCatalog.IntentsFor(category));
        }
    }

    /// <summary>
    /// GUI-9B §1, §11 — a static-analysis guard: the catalog and its source file must never
    /// reference any Risk/Assessment-derived type. This is the closest thing to a compile-time
    /// enforcement of "migration intent is inventory-derived, not Risk-derived" available without
    /// a Roslyn analyzer — it inspects the actual source file on disk.
    /// </summary>
    [Fact]
    public void CatalogSourceFile_ReferencesNoRiskOrAssessmentType()
    {
        var sourcePath = FindSourceFile("MigrationIntentCatalog.cs");
        var source = File.ReadAllText(sourcePath);

        string[] forbidden =
        [
            "RiskFinding", "MigrationIssue", "MigrationAction", "RiskSeverity",
            "MigrationStatus", "RiskRuleEngine", "MigrationPolicy", "BlockingIssue"
        ];

        foreach (var word in forbidden)
        {
            Assert.DoesNotContain(word, source, StringComparison.Ordinal);
        }
    }

    private static string FindSourceFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ServerSleuth.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var matches = Directory.GetFiles(dir!.FullName, fileName, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        return Assert.Single(matches);
    }
}
