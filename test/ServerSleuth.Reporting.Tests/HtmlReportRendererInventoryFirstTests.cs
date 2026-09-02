using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// GUI-8C — proves the HTML report is inventory-first: real discovered entities (DLLs, services,
/// runtimes, scheduled tasks, certificates, configuration) render as their own named sections
/// BEFORE the risk/migration-assessment sections, using the real
/// <see cref="ReportDtoMapper"/>/<see cref="HtmlReportRenderer"/> architecture — never a second
/// renderer, never fabricated data (§28-29 of the GUI-8C spec).
/// </summary>
public class HtmlReportRendererInventoryFirstTests
{
    private static string BuildHtml()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV\QINV_WEB_NOURM");
        var pool = EntityFactory.ApplicationPool("QINVAppPool");
        var app = EntityFactory.Application("QINV", "/TEST", @"C:\QINV\QINV_WEB_NOURM", poolId: pool.Id, siteId: site.Id);

        var dapper = EntityFactory.Dll(@"C:\QINV\QINV_WEB_NOURM\Bin\Dapper.dll", referencedBy: [app.Id]);
        var epplus = EntityFactory.Dll(@"C:\QINV\QINV_WEB_NOURM\Bin\EPPlus.dll", referencedBy: [app.Id]);

        var svc = EntityFactory.Service("QINVWorker", @"C:\QINV\Worker\QINVWorker.exe");
        var task = EntityFactory.ScheduledTask(@"\QINV\Nightly", @"C:\QINV\Worker\QINVWorker.exe");
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "8.0.4");
        var cert = EntityFactory.Certificate("qinv.example.com", "ABC123", validTo: DateTimeOffset.UtcNow.AddYears(1));
        var config = EntityFactory.Configuration(@"C:\QINV\QINV_WEB_NOURM\web.config", ownerEntityId: app.Id);

        var entities = new List<DiscoveryEntity>
        {
            site, pool, app, dapper, epplus, svc, task, runtime, cert, config
        };

        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        return new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
    }

    [Fact]
    public void RealComponentNames_AppearAsInventory_NotOnlyAsRiskFindings()
    {
        var html = BuildHtml();

        Assert.Contains("Dapper.dll", html, StringComparison.Ordinal);
        Assert.Contains("EPPlus.dll", html, StringComparison.Ordinal);
        Assert.Contains("QINVWorker", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DllBinariesSection_AppearsBeforeMigrationChecklistAndAssessmentSections()
    {
        var html = BuildHtml();

        var dllIndex = html.IndexOf("id=\"dll-binaries\"", StringComparison.Ordinal);
        var checklistIndex = html.IndexOf("id=\"migration-checklist\"", StringComparison.Ordinal);
        var actionsIndex = html.IndexOf("<h2>Actions", StringComparison.Ordinal);

        Assert.True(dllIndex >= 0);
        Assert.True(checklistIndex > dllIndex);
        if (actionsIndex >= 0)
        {
            Assert.True(actionsIndex > checklistIndex);
        }
    }

    [Fact]
    public void ApplicationsSection_AppearsBeforeInventorySections()
    {
        var html = BuildHtml();

        var applicationsIndex = html.IndexOf("id=\"applications\"", StringComparison.Ordinal);
        var dllIndex = html.IndexOf("id=\"dll-binaries\"", StringComparison.Ordinal);

        Assert.True(applicationsIndex >= 0);
        Assert.True(dllIndex > applicationsIndex);
    }

    [Fact]
    public void AllNineInventorySectionIds_AppearInDocumentOrder_WhenDataExists()
    {
        var html = BuildHtml();

        var expectedOrder = new[]
        {
            "id=\"dll-binaries\"",
            "id=\"windows-services\"",
            "id=\"runtime-requirements\"",
            "id=\"scheduled-tasks\"",
            "id=\"certificates\"",
            "id=\"configuration-files\"",
        };

        var indices = expectedOrder.Select(marker => html.IndexOf(marker, StringComparison.Ordinal)).ToList();
        foreach (var index in indices)
        {
            Assert.True(index >= 0);
        }

        for (var i = 1; i < indices.Count; i++)
        {
            Assert.True(indices[i] > indices[i - 1]);
        }
    }

    [Fact]
    public void MigrationChecklistSection_ListsOnlyCategoriesWithDiscoveredData()
    {
        var html = BuildHtml();

        var checklistStart = html.IndexOf("id=\"migration-checklist\"", StringComparison.Ordinal);
        Assert.True(checklistStart >= 0);
        var nextSection = html.IndexOf("<section", checklistStart + 1, StringComparison.Ordinal);
        var checklistSection = html[checklistStart..(nextSection >= 0 ? nextSection : html.Length)];

        // Categories with real discovered data in this fixture.
        Assert.Contains("Application Components (DLL / Binary)", checklistSection, StringComparison.Ordinal);
        Assert.Contains("Windows Services", checklistSection, StringComparison.Ordinal);
        Assert.Contains("Runtime Requirements", checklistSection, StringComparison.Ordinal);
        Assert.Contains("Certificates", checklistSection, StringComparison.Ordinal);

        // Category with zero discovered items (no COM components, no software, no external
        // connections in this fixture) must never appear — never fabricate an item (GUI-8C §四).
        Assert.DoesNotContain("COM Components", checklistSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Installed Software", checklistSection, StringComparison.Ordinal);
        Assert.DoesNotContain("External Connections", checklistSection, StringComparison.Ordinal);
    }

    /// <summary>
    /// GUI-9B: "Deploy" moved from forbidden to approved (it is the DLL/Binary intent in
    /// <c>MigrationIntentCatalog</c> — a descriptive label, never an execution verb) once the
    /// checklist's vocabulary became centrally sourced from that catalog (§4 of the GUI-9B
    /// instructions). The remaining forbidden words are real execution-implying verbs the
    /// catalog never uses for any category — this still fully enforces "intents describe, never
    /// execute" (skill.md GUI-9B §14).
    /// </summary>
    [Fact]
    public void MigrationChecklistSection_UsesOnlyApprovedActionVocabulary()
    {
        var html = BuildHtml();

        var checklistStart = html.IndexOf("id=\"migration-checklist\"", StringComparison.Ordinal);
        var nextSection = html.IndexOf("<section", checklistStart + 1, StringComparison.Ordinal);
        var checklistSection = html[checklistStart..(nextSection >= 0 ? nextSection : html.Length)];

        var forbidden = new[] { "Execute", "Delete", "Uninstall" };
        foreach (var word in forbidden)
        {
            Assert.DoesNotContain(word, checklistSection, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// GUI-9B §16: locks in the exact checklist wording now sourced from
    /// <c>MigrationIntentCatalog</c> instead of a local hard-coded string, so a future refactor
    /// of that catalog cannot silently change rendered report text without this test failing.
    /// </summary>
    [Fact]
    public void MigrationChecklistSection_ActionTextMatchesCentralizedIntentCatalog()
    {
        var html = BuildHtml();

        var checklistStart = html.IndexOf("id=\"migration-checklist\"", StringComparison.Ordinal);
        var nextSection = html.IndexOf("<section", checklistStart + 1, StringComparison.Ordinal);
        var checklistSection = html[checklistStart..(nextSection >= 0 ? nextSection : html.Length)];

        Assert.Contains("<td>Application Components (DLL / Binary)</td><td>2</td><td>Deploy / Verify</td>", checklistSection, StringComparison.Ordinal);
        Assert.Contains("<td>Runtime Requirements</td><td>1</td><td>Install / Verify</td>", checklistSection, StringComparison.Ordinal);
        Assert.Contains("<td>Windows Services</td><td>1</td><td>Create / Configure / Verify</td>", checklistSection, StringComparison.Ordinal);
        Assert.Contains("<td>Scheduled Tasks</td><td>1</td><td>Create / Configure / Verify</td>", checklistSection, StringComparison.Ordinal);
        Assert.Contains("<td>Certificates</td><td>1</td><td>Install / Verify</td>", checklistSection, StringComparison.Ordinal);
        Assert.Contains("<td>Configuration</td><td>1</td><td>Create / Configure / Verify</td>", checklistSection, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyDiscovery_RendersNoInventoryOrChecklistSections()
    {
        var entities = new List<DiscoveryEntity>();
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        var html = new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;

        Assert.DoesNotContain("id=\"dll-binaries\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"migration-checklist\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OmittingInventoryParameters_KeepsBackwardCompatibleOutput_WithNoInventorySections()
    {
        var site = EntityFactory.Site("QINV", @"C:\QINV\QINV_WEB_NOURM");
        var entities = new List<DiscoveryEntity> { site };
        var report = TestPipeline.Run(entities);

        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.DoesNotContain("id=\"dll-binaries\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"migration-checklist\"", html, StringComparison.Ordinal);
    }
}
