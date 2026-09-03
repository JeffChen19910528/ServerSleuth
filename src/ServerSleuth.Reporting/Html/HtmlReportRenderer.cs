using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Preparation;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Reporting.Json.Dto;

namespace ServerSleuth.Reporting.Html;

/// <summary>
/// Renders a <see cref="ServerMigrationAssessmentReport"/> as a single, self-contained,
/// offline-capable HTML document. Report is structured as Inventory-first, then Migration
/// Assessment — answering "what is on this server?" before "what are the migration risks?".
///
/// Supports <see cref="ReportLanguage.En"/> (default, backward-compatible) and
/// <see cref="ReportLanguage.ZhTw"/> (Traditional Chinese UI labels). Data values such as
/// service names, file paths, and software names are never translated — only UI labels,
/// headings, column headers, and category display strings.
///
/// All dynamic text is passed through <see cref="HtmlEncoder"/> with <see cref="UnicodeRanges.All"/>
/// before being written — HTML-required characters are always escaped, while readable Unicode
/// (Traditional Chinese, Latin-1 accents) is preserved as literal characters rather than
/// numeric entity references.
/// </summary>
public sealed class HtmlReportRenderer : IReportRenderer
{
    private readonly DateTimeOffset? _generatedAt;
    private readonly AggregateDiscoveryResult? _discovery;
    private readonly IReadOnlyList<ApplicationBoundary>? _boundaries;
    private readonly IReadOnlyList<ExternalDependency>? _externalDependencies;
    private readonly ReportLanguage _language;

    public HtmlReportRenderer(
        DateTimeOffset? generatedAt = null,
        AggregateDiscoveryResult? discovery = null,
        IReadOnlyList<ApplicationBoundary>? boundaries = null,
        IReadOnlyList<ExternalDependency>? externalDependencies = null,
        ReportLanguage language = ReportLanguage.En)
    {
        _generatedAt = generatedAt;
        _discovery = discovery;
        _boundaries = boundaries;
        _externalDependencies = externalDependencies;
        _language = language;
    }

    public ReportFormat Format => ReportFormat.Html;

    public ReportRenderResult Render(ServerMigrationAssessmentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var dto = _discovery is not null && _boundaries is not null && _externalDependencies is not null
            ? ReportDtoMapper.ToDto(report, _discovery, _boundaries, _externalDependencies)
            : ReportDtoMapper.ToDto(report);

        return new ReportRenderResult
        {
            Format = ReportFormat.Html,
            Content = BuildDocument(dto),
            Encoding = Encoding.UTF8
        };
    }

    // -------------------------------------------------------------------------
    // Document builder
    // -------------------------------------------------------------------------

    private string BuildDocument(ServerReportDto dto)
    {
        var sb = new StringBuilder();
        var hasInventory = dto.Services.Count + dto.Software.Count + dto.DllBinaries.Count +
                           dto.Runtimes.Count + dto.ComComponents.Count + dto.ScheduledTasks.Count +
                           dto.Certificates.Count + dto.Configurations.Count + dto.ExternalConnections.Count > 0;

        var lang = _language == ReportLanguage.ZhTw ? "zh-TW" : "en";
        sb.Append("<!doctype html>\n<html lang=\"").Append(lang).Append("\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(Esc(L("page-title"))).Append("</title>\n");
        sb.Append("<style>\n").Append(HtmlDocumentStyles.Css).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n<main>\n");

        sb.Append("<h1>").Append(Esc(L("page-title"))).Append("</h1>\n");
        if (_generatedAt is { } generatedAt)
        {
            sb.Append("<p class=\"muted\">Generated: ")
              .Append(Esc(generatedAt.ToString("u", CultureInfo.InvariantCulture)))
              .Append("</p>\n");
        }

        // 1. Inventory Dashboard — clickable stat cards (only when inventory data is present)
        if (hasInventory) AppendInventoryDashboard(sb, dto);

        // 2. Server Overview — migration status badges + summary counts
        AppendServerOverview(sb, dto, hasInventory);

        // 3. Coverage — scanner coverage and warnings
        AppendCoverage(sb, dto);

        // 4. Applications — per-application inventory and assessment
        AppendApplications(sb, dto.Applications);

        // 5-13. Inventory sections — IDs and ordering preserved for test compatibility.
        //  dll-binaries → windows-services → com-components → installed-software →
        //  runtime-requirements → scheduled-tasks → certificates → configuration-files →
        //  external-connections
        AppendInventorySection(sb, "dll-binaries", L("dll-binaries"), dto.DllBinaries);
        AppendServiceInventory(sb, dto.Services);
        AppendComInventory(sb, dto.ComComponents);
        AppendSoftwareInventory(sb, dto.Software);
        AppendInventorySection(sb, "runtime-requirements", L("runtime-requirements"), dto.Runtimes);
        AppendTaskInventory(sb, dto.ScheduledTasks);
        AppendInventorySection(sb, "certificates", L("certificates"), dto.Certificates);
        AppendInventorySection(sb, "configuration-files", L("configuration-files"), dto.Configurations);
        AppendInventorySection(sb, "external-connections", L("external-connections"), dto.ExternalConnections);

        // 14. Migration Assessment group header
        AppendMigrationAssessmentHeader(sb);

        // 15-22. Migration Assessment sections
        AppendMigrationChecklist(sb, dto);
        AppendActions(sb, dto.Actions);
        AppendChecks(sb, L("pre-migration-checks"), dto.PreMigrationChecks);
        AppendChecks(sb, L("post-migration-checks"), dto.PostMigrationChecks);
        AppendIssueList(sb, L("server-level-issues"), dto.ServerLevelIssues);
        AppendSharedInfrastructure(sb, dto.SharedInfrastructure);
        AppendDependencyGroups(sb, dto.Dependencies);
        AppendGraphValidationErrors(sb, dto.GraphValidationErrors);
        AppendDiagnostics(sb, dto.Diagnostics);

        sb.Append("</main>\n");
        AppendInlineScript(sb);
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Inventory Dashboard
    // -------------------------------------------------------------------------

    private void AppendInventoryDashboard(StringBuilder sb, ServerReportDto dto)
    {
        var runningServices = dto.Services.Count(
            s => string.Equals(s.Status, "Running", StringComparison.OrdinalIgnoreCase));

        sb.Append("<section id=\"inventory-dashboard\">\n");
        sb.Append("<h2>").Append(Esc(L("inventory-overview"))).Append("</h2>\n");
        sb.Append("<div class=\"grid\">\n");

        AppendDashboardStat(sb, dto.Applications.Count, L("stat-applications"), "#applications");
        AppendDashboardStat(sb, dto.Software.Count, L("stat-software"), "#installed-software");
        AppendDashboardStat(sb, dto.Services.Count, L("stat-services"), "#windows-services");
        AppendDashboardStat(sb, runningServices, L("stat-running-services"), "#windows-services");
        AppendDashboardStat(sb, dto.ScheduledTasks.Count, L("stat-scheduled-tasks"), "#scheduled-tasks");
        AppendDashboardStat(sb, dto.Runtimes.Count, L("stat-runtimes"), "#runtime-requirements");
        AppendDashboardStat(sb, dto.Certificates.Count, L("stat-certificates"), "#certificates");
        AppendDashboardStat(sb, dto.ComComponents.Count, L("stat-com-components"), "#com-components");
        AppendDashboardStat(sb, dto.DllBinaries.Count, L("stat-dll-binaries"), "#dll-binaries");
        AppendDashboardStat(sb, dto.Configurations.Count, L("stat-configurations"), "#configuration-files");
        AppendDashboardStat(sb, dto.ExternalConnections.Count, L("stat-external-connections"), "#external-connections");

        sb.Append("</div>\n</section>\n");
    }

    private static void AppendDashboardStat(StringBuilder sb, int count, string label, string href)
    {
        if (count == 0) return;
        sb.Append("<a href=\"").Append(href).Append("\" class=\"stat stat-link\">");
        sb.Append("<div class=\"value\">").Append(count).Append("</div>");
        sb.Append("<div class=\"label\">").Append(Esc(label)).Append("</div>");
        sb.Append("</a>\n");
    }

    // -------------------------------------------------------------------------
    // Server Overview (formerly Executive Summary — same id for compatibility)
    // -------------------------------------------------------------------------

    private void AppendServerOverview(StringBuilder sb, ServerReportDto dto, bool hasInventory)
    {
        var s = dto.Server;
        sb.Append("<section id=\"executive-summary\">\n");
        sb.Append("<h2>").Append(Esc(L("server-overview"))).Append("</h2>\n");
        sb.Append("<div class=\"panel\">\n");
        sb.Append("<p>").Append(Badge("status", s.OverallMigrationStatus))
          .Append(' ').Append(Badge("severity", s.OverallRiskSeverity)).Append("</p>\n");
        sb.Append("<div class=\"grid\">\n");

        if (hasInventory)
        {
            // Condensed: dashboard already shows full inventory counts
            AppendStat(sb, L("stat-applications"), s.ApplicationCount);
            AppendStat(sb, L("ma-blocking-issues"), s.BlockingIssueCount);
            AppendStat(sb, L("ma-actions"), s.ActionCount);
            AppendStat(sb, L("ma-checks"), s.VerificationCheckCount);
        }
        else
        {
            // Full stat grid when no inventory data (backward-compatible for tests)
            AppendStat(sb, "Applications", s.ApplicationCount);
            AppendStat(sb, "Blocked Applications", s.BlockedApplicationCount);
            AppendStat(sb, "Needs Remediation", s.NeedsRemediationApplicationCount);
            AppendStat(sb, "Ready With Conditions", s.ReadyWithConditionsApplicationCount);
            AppendStat(sb, "Ready", s.ReadyApplicationCount);
            AppendStat(sb, "Blocking Issues", s.BlockingIssueCount);
            AppendStat(sb, "Remediation Issues", s.RemediationIssueCount);
            AppendStat(sb, "Conditional Dependencies", s.ConditionalDependencyCount);
            AppendStat(sb, "Actions", s.ActionCount);
            AppendStat(sb, "Verification Checks", s.VerificationCheckCount);
            AppendStat(sb, "Dependencies", s.DependencyCount);
            AppendStat(sb, "Affected Entities", s.AffectedEntityCount);
            AppendStat(sb, "Affected Boundaries", s.AffectedBoundaryCount);
        }

        sb.Append("</div>\n</div>\n</section>\n");
    }

    private static void AppendStat(StringBuilder sb, string label, int value)
    {
        sb.Append("<div class=\"stat\"><div class=\"value\">").Append(value)
          .Append("</div><div class=\"label\">").Append(Esc(label)).Append("</div></div>\n");
    }

    // -------------------------------------------------------------------------
    // Migration Assessment group header
    // -------------------------------------------------------------------------

    private void AppendMigrationAssessmentHeader(StringBuilder sb)
    {
        sb.Append("<section id=\"migration-assessment\">\n");
        sb.Append("<h2>").Append(Esc(L("migration-assessment"))).Append("</h2>\n");
        sb.Append("<p class=\"muted\">").Append(Esc(L("migration-assessment-desc"))).Append("</p>\n");
        sb.Append("</section>\n");
    }

    // -------------------------------------------------------------------------
    // Coverage
    // -------------------------------------------------------------------------

    private void AppendCoverage(StringBuilder sb, ServerReportDto dto)
    {
        sb.Append("<section id=\"coverage\">\n<h2>").Append(Esc(L("coverage"))).Append("</h2>\n<div class=\"panel\">\n");
        sb.Append("<p>").Append(Badge("coverage", dto.Coverage)).Append("</p>\n");
        sb.Append("<p class=\"muted\">").Append(Esc(L("coverage-desc"))).Append("</p>\n");

        if (dto.CoverageWarnings.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-no-coverage-warnings"))).Append("</p>\n");
        }
        else
        {
            sb.Append("<table>\n<thead><tr>");
            sb.Append("<th>").Append(Esc(L("cov-col-scanner"))).Append("</th>");
            sb.Append("<th>").Append(Esc(L("cov-col-scanner-status"))).Append("</th>");
            sb.Append("<th>").Append(Esc(L("cov-col-platform"))).Append("</th>");
            sb.Append("<th>").Append(Esc(L("cov-col-reason"))).Append("</th>");
            sb.Append("<th>").Append(Esc(L("cov-col-evidence"))).Append("</th>");
            sb.Append("</tr></thead>\n<tbody>\n");
            foreach (var w in dto.CoverageWarnings)
            {
                sb.Append("<tr>");
                sb.Append("<td><code>").Append(Esc(w.ScannerId)).Append("</code></td>");
                sb.Append("<td>").Append(Esc(w.ScannerStatus)).Append("</td>");
                sb.Append("<td>").Append(Esc(w.AffectedPlatform)).Append("</td>");
                sb.Append("<td>").Append(Esc(w.Reason)).Append("</td>");
                sb.Append("<td>").Append(StringListToTags(w.Evidence)).Append("</td>");
                sb.Append("</tr>\n");
            }
            sb.Append("</tbody>\n</table>\n");
        }

        sb.Append("</div>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Applications
    // -------------------------------------------------------------------------

    private void AppendApplications(StringBuilder sb, IReadOnlyList<ApplicationDto> applications)
    {
        sb.Append("<section id=\"applications\">\n<h2>").Append(Esc(L("applications"))).Append("</h2>\n");
        if (applications.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-no-findings"))).Append("</p>\n</section>\n");
            return;
        }

        foreach (var app in applications)
        {
            sb.Append("<details class=\"panel\" open>\n<summary>")
              .Append(Esc(app.ApplicationName))
              .Append(" (<code>").Append(Esc(app.BoundaryId)).Append("</code>) ")
              .Append(Badge("status", app.MigrationStatus)).Append(' ')
              .Append(Badge("severity", app.RiskSeverity))
              .Append("</summary>\n");

            sb.Append("<p class=\"muted\">").Append(Esc(L("app-affected-entities"))).Append(" ")
              .Append(app.AffectedEntityCount)
              .Append(" &middot; ").Append(Esc(L("app-affected-boundaries"))).Append(" ")
              .Append(app.AffectedBoundaryCount).Append("</p>\n");

            AppendIssueTable(sb, app.Issues);
            AppendDependencyTable(sb, app.Dependencies);
            AppendActionTable(sb, app.Actions);
            AppendCheckTable(sb, L("app-pre-migration-checks"), app.PreMigrationChecks);
            AppendCheckTable(sb, L("app-post-migration-checks"), app.PostMigrationChecks);

            sb.Append("</details>\n");
        }

        sb.Append("</section>\n");
    }

    // -------------------------------------------------------------------------
    // Enhanced Windows Services Inventory (id="windows-services")
    // -------------------------------------------------------------------------

    private void AppendServiceInventory(StringBuilder sb, IReadOnlyList<InventoryEntityDto> services)
    {
        if (services.Count == 0) return;

        var runningCount = services.Count(
            s => string.Equals(s.Status, "Running", StringComparison.OrdinalIgnoreCase));
        var systemCount = services.Count(IsWindowsSystemService);
        var thirdPartyCount = services.Count - systemCount;

        sb.Append("<section id=\"windows-services\">\n");
        sb.Append("<h2>").Append(Esc(L("windows-services"))).Append(" (").Append(services.Count).Append(")</h2>\n");

        sb.Append("<div class=\"inventory-stats\">");
        sb.Append("<span>").Append(Esc(L("filter-running"))).Append(": <strong>").Append(runningCount).Append("</strong></span> ");
        sb.Append("<span>").Append(Esc(L("cat-windows-system"))).Append(": <strong>").Append(systemCount).Append("</strong></span> ");
        sb.Append("<span>").Append(Esc(L("cat-third-party"))).Append(": <strong>").Append(thirdPartyCount).Append("</strong></span>");
        sb.Append("</div>\n");

        sb.Append("<div class=\"inventory-controls\">\n");
        AppendFilterGroup(sb, "svc-table", "status", [
            ("", L("filter-all")),
            ("Running", L("filter-running")),
            ("Stopped", L("filter-stopped")),
        ]);
        AppendFilterGroup(sb, "svc-table", "category", [
            ("", L("filter-all")),
            ("windows-system", L("cat-windows-system")),
            ("third-party", L("cat-third-party")),
        ]);
        AppendSearchBox(sb, "svc-table");
        sb.Append("</div>\n");

        sb.Append("<details class=\"panel\">\n<summary>")
          .Append(Esc(L("windows-services"))).Append(" — ")
          .Append(services.Count).Append(services.Count == 1 ? " item" : " items")
          .Append("</summary>\n");

        sb.Append("<table id=\"svc-table\">\n<thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-category"));
        AppendTh(sb, L("col-status"));
        AppendTh(sb, L("col-start-type"));
        AppendTh(sb, L("col-account"));
        AppendTh(sb, L("col-path"));
        AppendTh(sb, L("col-application"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var svc in services)
        {
            var category = GetServiceCategory(svc);
            var status = svc.Status ?? string.Empty;
            sb.Append("<tr data-status=\"").Append(Esc(status))
              .Append("\" data-category=\"").Append(category).Append("\">");

            sb.Append("<td><strong>").Append(Esc(svc.Name)).Append("</strong>");
            if (!string.IsNullOrEmpty(svc.DisplayName) && svc.DisplayName != svc.Name)
                sb.Append("<br><span class=\"muted\">").Append(Esc(svc.DisplayName)).Append("</span>");
            sb.Append("</td>");

            sb.Append("<td>").Append(CategoryBadge(category)).Append("</td>");

            sb.Append("<td>");
            if (!string.IsNullOrEmpty(status))
                sb.Append(StatusInline(status));
            else
                sb.Append("&mdash;");
            sb.Append("</td>");

            sb.Append("<td>").Append(Esc(svc.StartType)).Append("</td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(svc.ServiceAccount)).Append("</span></td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(svc.ExecutablePath ?? svc.Path)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(svc.ApplicationName)).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</details>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Enhanced COM Components Inventory (id="com-components")
    // -------------------------------------------------------------------------

    private void AppendComInventory(StringBuilder sb, IReadOnlyList<InventoryEntityDto> comComponents)
    {
        if (comComponents.Count == 0) return;

        var systemCount = comComponents.Count(c => GetComCategory(c) == "windows-system");
        var msCount = comComponents.Count(c => GetComCategory(c) == "microsoft");
        var thirdCount = comComponents.Count - systemCount - msCount;

        sb.Append("<section id=\"com-components\">\n");
        sb.Append("<h2>").Append(Esc(L("com-components"))).Append(" (").Append(comComponents.Count).Append(")</h2>\n");

        sb.Append("<div class=\"inventory-stats\">");
        sb.Append("<span>").Append(Esc(L("cat-windows-system"))).Append(": <strong>").Append(systemCount).Append("</strong></span> ");
        sb.Append("<span>").Append(Esc(L("cat-microsoft"))).Append(": <strong>").Append(msCount).Append("</strong></span> ");
        sb.Append("<span>").Append(Esc(L("cat-third-party"))).Append(": <strong>").Append(thirdCount).Append("</strong></span>");
        sb.Append("</div>\n");

        sb.Append("<div class=\"inventory-controls\">\n");
        AppendFilterGroup(sb, "com-table", "category", [
            ("", L("filter-all")),
            ("windows-system", L("cat-windows-system")),
            ("microsoft", L("cat-microsoft")),
            ("third-party", L("cat-third-party")),
        ]);
        AppendSearchBox(sb, "com-table");
        sb.Append("</div>\n");

        sb.Append("<details class=\"panel\">\n<summary>")
          .Append(Esc(L("com-components"))).Append(" — ")
          .Append(comComponents.Count).Append(comComponents.Count == 1 ? " item" : " items")
          .Append("</summary>\n");

        sb.Append("<table id=\"com-table\">\n<thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-category"));
        AppendTh(sb, "CLSID / ProgID");
        AppendTh(sb, L("col-path"));
        AppendTh(sb, L("col-application"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var com in comComponents)
        {
            var category = GetComCategory(com);
            sb.Append("<tr data-category=\"").Append(category).Append("\">");

            sb.Append("<td><strong>").Append(Esc(com.Name)).Append("</strong>");
            if (!string.IsNullOrEmpty(com.ThreadingModel))
                sb.Append("<br><span class=\"muted\">").Append(Esc(com.ThreadingModel)).Append("</span>");
            sb.Append("</td>");

            sb.Append("<td>").Append(CategoryBadge(category)).Append("</td>");

            sb.Append("<td>");
            if (!string.IsNullOrEmpty(com.Clsid))
                sb.Append("<code>").Append(Esc(com.Clsid)).Append("</code>");
            if (!string.IsNullOrEmpty(com.ProgId))
                sb.Append("<br><span class=\"muted\">").Append(Esc(com.ProgId)).Append("</span>");
            sb.Append("</td>");

            var comPath = com.InprocServer32 ?? com.Path;
            sb.Append("<td><span class=\"muted\">").Append(Esc(comPath)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(com.ApplicationName)).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</details>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Enhanced Installed Software Inventory (id="installed-software")
    // -------------------------------------------------------------------------

    private void AppendSoftwareInventory(StringBuilder sb, IReadOnlyList<InventoryEntityDto> software)
    {
        if (software.Count == 0) return;

        var msCount = software.Count(s => IsMicrosoftPublisher(s.Publisher));
        var thirdCount = software.Count - msCount;

        sb.Append("<section id=\"installed-software\">\n");
        sb.Append("<h2>").Append(Esc(L("installed-software"))).Append(" (").Append(software.Count).Append(")</h2>\n");

        sb.Append("<div class=\"inventory-stats\">");
        sb.Append("<span>").Append(Esc(L("cat-microsoft"))).Append(": <strong>").Append(msCount).Append("</strong></span> ");
        sb.Append("<span>").Append(Esc(L("cat-third-party"))).Append(": <strong>").Append(thirdCount).Append("</strong></span>");
        sb.Append("</div>\n");

        sb.Append("<div class=\"inventory-controls\">\n");
        AppendFilterGroup(sb, "sw-table", "category", [
            ("", L("filter-all")),
            ("microsoft", L("cat-microsoft")),
            ("third-party", L("cat-third-party")),
        ]);
        AppendSearchBox(sb, "sw-table");
        sb.Append("</div>\n");

        sb.Append("<details class=\"panel\">\n<summary>")
          .Append(Esc(L("installed-software"))).Append(" — ")
          .Append(software.Count).Append(software.Count == 1 ? " item" : " items")
          .Append("</summary>\n");

        sb.Append("<table id=\"sw-table\">\n<thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-version"));
        AppendTh(sb, L("col-publisher"));
        AppendTh(sb, L("col-install-date"));
        AppendTh(sb, "Architecture");
        AppendTh(sb, L("col-path"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var sw in software)
        {
            var category = IsMicrosoftPublisher(sw.Publisher) ? "microsoft" : "third-party";
            sb.Append("<tr data-category=\"").Append(category).Append("\">");

            sb.Append("<td><strong>").Append(Esc(sw.Name)).Append("</strong></td>");
            sb.Append("<td>").Append(Esc(sw.Version)).Append("</td>");
            sb.Append("<td>").Append(Esc(sw.Publisher)).Append("</td>");
            sb.Append("<td>").Append(Esc(sw.InstallDate)).Append("</td>");
            sb.Append("<td>").Append(Esc(sw.Architecture)).Append("</td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(sw.InstallLocation ?? sw.Path)).Append("</span></td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</details>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Enhanced Scheduled Tasks Inventory (id="scheduled-tasks")
    // -------------------------------------------------------------------------

    private void AppendTaskInventory(StringBuilder sb, IReadOnlyList<InventoryEntityDto> tasks)
    {
        if (tasks.Count == 0) return;

        var systemCount = tasks.Count(IsWindowsSystemTask);
        var customCount = tasks.Count - systemCount;

        sb.Append("<section id=\"scheduled-tasks\">\n");
        sb.Append("<h2>").Append(Esc(L("scheduled-tasks"))).Append(" (").Append(tasks.Count).Append(")</h2>\n");

        sb.Append("<div class=\"inventory-stats\">");
        sb.Append("<span>").Append(Esc(L("cat-windows-system"))).Append(": <strong>").Append(systemCount).Append("</strong></span> ");
        sb.Append("<span>").Append(Esc(L("cat-custom"))).Append(": <strong>").Append(customCount).Append("</strong></span>");
        sb.Append("</div>\n");

        sb.Append("<div class=\"inventory-controls\">\n");
        AppendFilterGroup(sb, "task-table", "category", [
            ("", L("filter-all")),
            ("windows-system", L("cat-windows-system")),
            ("custom", L("cat-custom")),
        ]);
        AppendSearchBox(sb, "task-table");
        sb.Append("</div>\n");

        sb.Append("<details class=\"panel\">\n<summary>")
          .Append(Esc(L("scheduled-tasks"))).Append(" — ")
          .Append(tasks.Count).Append(tasks.Count == 1 ? " item" : " items")
          .Append("</summary>\n");

        sb.Append("<table id=\"task-table\">\n<thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-category"));
        AppendTh(sb, L("col-status"));
        AppendTh(sb, L("col-run-as"));
        AppendTh(sb, L("col-action"));
        AppendTh(sb, L("col-trigger"));
        AppendTh(sb, L("col-application"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var task in tasks)
        {
            var category = GetTaskCategory(task);
            var enabled = string.Equals(task.Enabled, "True", StringComparison.OrdinalIgnoreCase);
            sb.Append("<tr data-category=\"").Append(category).Append("\">");

            sb.Append("<td><strong>").Append(Esc(task.Name)).Append("</strong>");
            if (!string.IsNullOrEmpty(task.Folder))
                sb.Append("<br><span class=\"muted\">").Append(Esc(task.Folder)).Append("</span>");
            sb.Append("</td>");

            sb.Append("<td>").Append(CategoryBadge(category)).Append("</td>");

            sb.Append("<td>");
            if (!enabled)
                sb.Append("<span class=\"status-disabled\">Disabled</span>");
            else
                sb.Append("<span class=\"status-enabled\">Enabled</span>");
            sb.Append("</td>");

            sb.Append("<td><span class=\"muted\">").Append(Esc(task.RunAsAccount)).Append("</span></td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(task.TaskAction)).Append("</span></td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(task.Trigger)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(task.ApplicationName)).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</details>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Generic Inventory Section (DLL, Runtime, Certificate, Config, External)
    // -------------------------------------------------------------------------

    private void AppendInventorySection(
        StringBuilder sb, string id, string title, IReadOnlyList<InventoryEntityDto> entities)
    {
        if (entities.Count == 0) return;

        sb.Append("<section id=\"").Append(id).Append("\">\n");
        sb.Append("<h2>").Append(Esc(title)).Append(" (").Append(entities.Count).Append(")</h2>\n");
        sb.Append("<details class=\"panel\">\n<summary>").Append(Esc(title)).Append(" — ")
          .Append(entities.Count).Append(entities.Count == 1 ? " item" : " items").Append("</summary>\n");

        sb.Append("<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("col-name"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("col-application"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("col-version"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("col-path"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("col-status"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var e in entities)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(e.Name)).Append("</strong>");
            if (!string.IsNullOrEmpty(e.Architecture))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Architecture)).Append("</span>");
            if (!string.IsNullOrEmpty(e.DisplayName) && e.DisplayName != e.Name)
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.DisplayName)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Subject))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Subject)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Clsid))
                sb.Append("<br><code>").Append(Esc(e.Clsid)).Append("</code>");
            if (!string.IsNullOrEmpty(e.Kind))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Kind)).Append("</span>");
            sb.Append("</td>");

            sb.Append("<td>").Append(Esc(e.ApplicationName)).Append("</td>");

            sb.Append("<td>");
            if (!string.IsNullOrEmpty(e.Version)) sb.Append(Esc(e.Version));
            if (!string.IsNullOrEmpty(e.StartType))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.StartType)).Append("</span>");
            if (!string.IsNullOrEmpty(e.ServiceAccount))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.ServiceAccount)).Append("</span>");
            if (!string.IsNullOrEmpty(e.ThreadingModel))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.ThreadingModel)).Append("</span>");
            if (!string.IsNullOrEmpty(e.ValidTo))
                sb.Append("<br><span class=\"muted\">Valid to: ").Append(Esc(e.ValidTo)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Folder))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Folder)).Append("</span>");
            if (!string.IsNullOrEmpty(e.RunAsAccount))
                sb.Append("<br><span class=\"muted\">Run as: ").Append(Esc(e.RunAsAccount)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Format))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Format)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Publisher))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Publisher)).Append("</span>");
            if (!string.IsNullOrEmpty(e.InstallDate))
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.InstallDate)).Append("</span>");
            if (!string.IsNullOrEmpty(e.Endpoint))
                sb.Append("<br><code>").Append(Esc(e.Endpoint)).Append("</code>");
            sb.Append("</td>");

            var pathValue = !string.IsNullOrEmpty(e.InstallLocation) ? e.InstallLocation
                : !string.IsNullOrEmpty(e.ExecutablePath) ? e.ExecutablePath
                : e.Path;
            sb.Append("<td><span class=\"muted\">").Append(Esc(pathValue)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(e.Status)).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</details>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Migration Checklist
    // -------------------------------------------------------------------------

    private void AppendMigrationChecklist(StringBuilder sb, ServerReportDto dto)
    {
        static string Action(string category) =>
            string.Join(" / ", MigrationIntentCatalog.IntentsFor(category));

        var rows = new (string Category, int Count, string MigAction)[]
        {
            (L("chk-dll"), dto.DllBinaries.Count, Action("Dll")),
            (L("chk-runtime"), dto.Runtimes.Count, Action("Runtime")),
            (L("chk-service"), dto.Services.Count, Action("Service")),
            (L("chk-com"), dto.ComComponents.Count, Action("ComComponent")),
            (L("chk-software"), dto.Software.Count, Action("Software")),
            (L("chk-task"), dto.ScheduledTasks.Count, Action("ScheduledTask")),
            (L("chk-cert"), dto.Certificates.Count, Action("Certificate")),
            (L("chk-config"), dto.Configurations.Count, Action("Configuration")),
            (L("chk-external"), dto.ExternalConnections.Count, Action("ExternalDependency")),
        };

        var applicable = rows.Where(r => r.Count > 0).ToList();
        if (applicable.Count == 0) return;

        sb.Append("<section id=\"migration-checklist\">\n<h2>").Append(Esc(L("migration-checklist"))).Append("</h2>\n");
        sb.Append("<p class=\"muted\">").Append(Esc(L("migration-checklist-desc"))).Append("</p>\n");
        sb.Append("<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("chk-col-category"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("chk-col-discovered"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("chk-col-action"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var row in applicable)
        {
            sb.Append("<tr><td>").Append(Esc(row.Category)).Append("</td><td>").Append(row.Count)
              .Append("</td><td>").Append(Esc(row.MigAction)).Append("</td></tr>\n");
        }

        sb.Append("</tbody>\n</table>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Migration Actions
    // -------------------------------------------------------------------------

    private void AppendActions(StringBuilder sb, IReadOnlyList<ActionDto> actions)
    {
        sb.Append("<section id=\"actions\">\n<h2>").Append(Esc(L("actions"))).Append("</h2>\n");
        if (actions.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-no-actions"))).Append("</p>\n</section>\n");
            return;
        }

        var review = actions.Where(a =>
            a.ActionType.StartsWith("Review", StringComparison.Ordinal) ||
            a.ActionType.StartsWith("Document", StringComparison.Ordinal)).ToList();
        var reviewIds = review.Select(a => a.ActionId).ToHashSet(StringComparer.Ordinal);
        var pre = actions.Where(a => !reviewIds.Contains(a.ActionId) && a.Phase == "PreMigration").ToList();
        var post = actions.Where(a => !reviewIds.Contains(a.ActionId) && a.Phase == "PostMigration").ToList();

        AppendActionSubsection(sb, L("section-pre-migration"), pre);
        AppendActionSubsection(sb, L("section-post-migration"), post);
        AppendActionSubsection(sb, L("section-review-docs"), review);

        sb.Append("</section>\n");
    }

    private void AppendActionSubsection(StringBuilder sb, string title, IReadOnlyList<ActionDto> actions)
    {
        sb.Append("<h3>").Append(Esc(title)).Append(" (").Append(actions.Count).Append(")</h3>\n");
        AppendActionTable(sb, actions);
    }

    private void AppendActionTable(StringBuilder sb, IReadOnlyList<ActionDto> actions)
    {
        if (actions.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n");
            return;
        }

        sb.Append("<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("act-col-action"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("act-col-type"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("act-col-priority"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("act-col-phase"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("act-col-related"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("act-col-evidence"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var a in actions)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(a.Title)).Append("</strong><br><span class=\"muted\">")
              .Append(Esc(a.Description)).Append("</span><br><code>").Append(Esc(a.ActionId)).Append("</code>")
              .Append("<br><span class=\"muted\">").Append(Esc(a.Rationale)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(a.ActionType)).Append("</td>");
            sb.Append("<td>").Append(Badge("priority", a.Priority)).Append("</td>");
            sb.Append("<td>").Append(Esc(a.Phase)).Append("</td>");
            sb.Append("<td>").Append(IdTags(a.AffectedBoundaryIds))
              .Append(IdTags(a.RelatedIssueIds)).Append(IdTags(a.RelatedDependencyIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(a.Evidence)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    // -------------------------------------------------------------------------
    // Verification Checks
    // -------------------------------------------------------------------------

    private void AppendChecks(StringBuilder sb, string title, IReadOnlyList<CheckDto> checks)
    {
        sb.Append("<section>\n<h2>").Append(Esc(title)).Append("</h2>\n");
        AppendCheckTable(sb, null, checks);
        sb.Append("</section>\n");
    }

    private void AppendCheckTable(StringBuilder sb, string? heading, IReadOnlyList<CheckDto> checks)
    {
        if (heading is not null)
            sb.Append("<h3>").Append(Esc(heading)).Append(" (").Append(checks.Count).Append(")</h3>\n");

        if (checks.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n");
            return;
        }

        sb.Append("<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("chk-col-check"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("chk-col-type"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("chk-col-phase"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("chk-col-related"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("chk-col-evidence"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var c in checks)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(c.Title)).Append("</strong><br><span class=\"muted\">")
              .Append(Esc(c.Description)).Append("</span><br><code>").Append(Esc(c.CheckId)).Append("</code>")
              .Append("<br><span class=\"muted\">").Append(Esc(c.Rationale)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(c.CheckType)).Append("</td>");
            sb.Append("<td>").Append(Esc(c.Phase)).Append("</td>");
            sb.Append("<td>").Append(IdTags(c.AffectedBoundaryIds))
              .Append(IdTags(c.RelatedActionIds)).Append(IdTags(c.RelatedDependencyIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(c.Evidence)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    // -------------------------------------------------------------------------
    // Issues
    // -------------------------------------------------------------------------

    private void AppendIssueList(StringBuilder sb, string title, IReadOnlyList<IssueDto> issues)
    {
        sb.Append("<section>\n<h2>").Append(Esc(title)).Append("</h2>\n");
        if (issues.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }
        AppendIssueTable(sb, issues);
        sb.Append("</section>\n");
    }

    private void AppendIssueTable(StringBuilder sb, IReadOnlyList<IssueDto> issues)
    {
        if (issues.Count == 0) return;

        sb.Append("<h3>Issues</h3>\n<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("iss-col-issue"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("iss-col-rule"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("iss-col-severity"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("iss-col-impact"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("iss-col-confidence"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("iss-col-affected"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("iss-col-evidence"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var i in issues)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(i.Title)).Append("</strong><br><span class=\"muted\">")
              .Append(Esc(i.Description)).Append("</span><br><code>").Append(Esc(i.IssueId)).Append("</code></td>");
            sb.Append("<td><code>").Append(Esc(i.RuleId)).Append("</code><br><span class=\"muted\">")
              .Append(Esc(i.SourceRiskFindingId)).Append("</span></td>");
            sb.Append("<td>").Append(Badge("severity", i.Severity)).Append("</td>");
            sb.Append("<td>").Append(Badge("impact", i.MigrationStatusImpact)).Append("</td>");
            sb.Append("<td>").Append(FormatConfidence(i.Confidence)).Append("</td>");
            sb.Append("<td>").Append(IdTags(i.AffectedBoundaryIds)).Append(IdTags(i.AffectedEntityIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(i.Evidence)).Append("</td>");
            sb.Append("</tr>\n");

            sb.Append("<tr><td colspan=\"7\"><span class=\"muted\">")
              .Append(Esc(L("iss-required-action"))).Append("</span> ").Append(Esc(i.RequiredAction))
              .Append("<br><span class=\"muted\">").Append(Esc(L("iss-policy-reason"))).Append("</span> ")
              .Append(Esc(i.PolicyDecisionReason)).Append("</td></tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    // -------------------------------------------------------------------------
    // Shared Infrastructure & Dependency Groups
    // -------------------------------------------------------------------------

    private void AppendSharedInfrastructure(StringBuilder sb, IReadOnlyList<DependencyDto> shared)
    {
        sb.Append("<section id=\"shared-infrastructure\">\n<h2>").Append(Esc(L("shared-infrastructure"))).Append("</h2>\n");
        if (shared.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-no-dep-shared"))).Append("</p>\n</section>\n");
            return;
        }
        sb.Append("<p class=\"muted\">").Append(Esc(L("shared-infrastructure-desc"))).Append("</p>\n");
        AppendDependencyTable(sb, shared);
        sb.Append("</section>\n");
    }

    private void AppendDependencyGroups(StringBuilder sb, IReadOnlyList<DependencyGroupDto> groups)
    {
        sb.Append("<section id=\"dependencies\">\n<h2>").Append(Esc(L("dependencies"))).Append("</h2>\n");
        if (groups.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-no-deps"))).Append("</p>\n</section>\n");
            return;
        }
        foreach (var group in groups)
        {
            sb.Append("<h3>").Append(Esc(group.Type)).Append(" (").Append(group.Dependencies.Count).Append(")</h3>\n");
            AppendDependencyTable(sb, group.Dependencies);
        }
        sb.Append("</section>\n");
    }

    private void AppendDependencyTable(StringBuilder sb, IReadOnlyList<DependencyDto> dependencies)
    {
        if (dependencies.Count == 0) return;

        sb.Append("<h3>").Append(Esc(L("dependencies"))).Append("</h3>\n<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("dep-col-dep"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("dep-col-type"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("dep-col-target"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("dep-col-phase"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("dep-col-confidence"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("dep-col-boundaries"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("dep-col-evidence"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var d in dependencies)
        {
            sb.Append("<tr>");
            sb.Append("<td><code>").Append(Esc(d.DependencyId)).Append("</code>")
              .Append(d.RelatedRiskFindingId is null ? string.Empty :
                  "<br><span class=\"muted\">Related: " + Esc(d.RelatedRiskFindingId) + "</span>")
              .Append("</td>");
            sb.Append("<td>").Append(Esc(d.Type)).Append("</td>");
            sb.Append("<td>").Append(Esc(d.Target)).Append("<br><span class=\"muted\">")
              .Append(Esc(d.VerificationRequirement)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(d.VerificationPhase)).Append("</td>");
            sb.Append("<td>").Append(FormatConfidence(d.Confidence)).Append("</td>");
            sb.Append("<td>").Append(IdTags(d.AffectedBoundaryIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(d.Evidence)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    // -------------------------------------------------------------------------
    // Graph Validation & Diagnostics
    // -------------------------------------------------------------------------

    private void AppendGraphValidationErrors(StringBuilder sb, IReadOnlyList<GraphValidationFindingDto> findings)
    {
        sb.Append("<section id=\"graph-validation\">\n<h2>").Append(Esc(L("graph-validation"))).Append("</h2>\n");
        if (findings.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-no-graph-errors"))).Append("</p>\n</section>\n");
            return;
        }

        sb.Append("<table>\n<thead><tr>");
        sb.Append("<th>").Append(Esc(L("gv-col-category"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("gv-col-code"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("gv-col-severity"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("gv-col-message"))).Append("</th>");
        sb.Append("<th>").Append(Esc(L("gv-col-entities"))).Append("</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var f in findings)
        {
            sb.Append("<tr>");
            sb.Append("<td>").Append(Esc(f.Category)).Append("</td>");
            sb.Append("<td><code>").Append(Esc(f.Code)).Append("</code></td>");
            sb.Append("<td>").Append(Esc(f.Severity)).Append("</td>");
            sb.Append("<td>").Append(Esc(f.Message)).Append("</td>");
            sb.Append("<td>").Append(IdTags(f.EntityIds)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n</section>\n");
    }

    private void AppendDiagnostics(StringBuilder sb, DiagnosticsDto d)
    {
        sb.Append("<section id=\"diagnostics\">\n<h2>").Append(Esc(L("diagnostics"))).Append("</h2>\n<div class=\"panel grid\">\n");
        AppendStat(sb, L("diag-apps"), d.ApplicationsConsolidated);
        AppendStat(sb, L("diag-server-issues"), d.ServerLevelIssueCount);
        AppendStat(sb, L("diag-shared-infra"), d.SharedInfrastructureDependencyCount);
        AppendStat(sb, L("diag-coverage-warnings"), d.CoverageWarningCount);
        AppendStat(sb, L("diag-graph-errors"), d.GraphValidationErrorCount);
        sb.Append("</div>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Inline JavaScript (search / filter)
    // -------------------------------------------------------------------------

    private static void AppendInlineScript(StringBuilder sb)
    {
        sb.Append("<script>\n");
        sb.Append("""
            (function(){
              function applyFilter(tableId,attr,value){
                var t=document.getElementById(tableId);
                if(!t)return;
                t.querySelectorAll('tbody tr').forEach(function(r){
                  r.style.display=(!value||r.dataset[attr]===value)?'':'none';
                });
              }
              function applySearch(tableId,term){
                var t=document.getElementById(tableId);
                if(!t)return;
                var lc=term.toLowerCase();
                t.querySelectorAll('tbody tr').forEach(function(r){
                  var txt=(r.cells[0]?r.cells[0].textContent:'').toLowerCase();
                  r.style.display=txt.includes(lc)?'':'none';
                });
              }
              function openDetails(tableId){
                var t=document.getElementById(tableId);
                if(t){var d=t.closest('details');if(d)d.open=true;}
              }
              window.invFilter=function(btn,tableId,attr,value){
                var g=btn.closest('.filter-group');
                if(g)g.querySelectorAll('.filter-btn').forEach(function(b){b.classList.remove('active');});
                btn.classList.add('active');
                applyFilter(tableId,attr,value);
                openDetails(tableId);
              };
              window.invSearch=function(input,tableId){
                applySearch(tableId,input.value);
                if(input.value)openDetails(tableId);
              };
            })();
            """);
        sb.Append("</script>\n");
    }

    // -------------------------------------------------------------------------
    // Filter control helpers
    // -------------------------------------------------------------------------

    private static void AppendFilterGroup(
        StringBuilder sb,
        string tableId,
        string attr,
        (string value, string label)[] buttons)
    {
        sb.Append("<div class=\"filter-group\">\n");
        var first = true;
        foreach (var (value, label) in buttons)
        {
            var active = first ? " active" : string.Empty;
            sb.Append("<button class=\"filter-btn").Append(active).Append("\" onclick=\"invFilter(this,'")
              .Append(tableId).Append("','").Append(attr).Append("','").Append(value).Append("')\">")
              .Append(Esc(label)).Append("</button>\n");
            first = false;
        }
        sb.Append("</div>\n");
    }

    private static void AppendSearchBox(StringBuilder sb, string tableId)
    {
        sb.Append("<input type=\"text\" class=\"search-box\" placeholder=\"Search...\" oninput=\"invSearch(this,'")
          .Append(tableId).Append("')\">\n");
    }

    private static void AppendTh(StringBuilder sb, string label)
    {
        sb.Append("<th>").Append(Esc(label)).Append("</th>");
    }

    // -------------------------------------------------------------------------
    // Classification helpers
    // -------------------------------------------------------------------------

    private static bool IsWindowsSystemService(InventoryEntityDto svc)
    {
        var path = svc.ExecutablePath ?? svc.Path ?? string.Empty;
        return path.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(@"%SystemRoot%\", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(@"%WinDir%\", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetServiceCategory(InventoryEntityDto svc) =>
        IsWindowsSystemService(svc) ? "windows-system" : "third-party";

    private static bool IsWindowsSystemTask(InventoryEntityDto task)
    {
        var folder = task.Folder ?? string.Empty;
        return folder.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTaskCategory(InventoryEntityDto task) =>
        IsWindowsSystemTask(task) ? "windows-system" : "custom";

    private static string GetComCategory(InventoryEntityDto com)
    {
        var path = com.InprocServer32 ?? com.Path ?? string.Empty;
        if (path.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(@"%SystemRoot%\", StringComparison.OrdinalIgnoreCase))
            return "windows-system";
        if (path.StartsWith(@"C:\Program Files\Microsoft", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(@"C:\Program Files (x86)\Microsoft", StringComparison.OrdinalIgnoreCase))
            return "microsoft";
        return "third-party";
    }

    private static bool IsMicrosoftPublisher(string? publisher)
    {
        if (string.IsNullOrEmpty(publisher)) return false;
        return publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
    }

    private string CategoryBadge(string category)
    {
        var (cssClass, label) = category switch
        {
            "windows-system" => ("cat-system", L("cat-windows-system")),
            "microsoft" => ("cat-microsoft", L("cat-microsoft")),
            "custom" => ("cat-custom", L("cat-custom")),
            _ => ("cat-third-party", L("cat-third-party")),
        };
        return "<span class=\"cat-badge " + cssClass + "\">" + Esc(label) + "</span>";
    }

    private static string StatusInline(string status)
    {
        var css = status.ToLowerInvariant() switch
        {
            "running" => "status-running",
            "stopped" => "status-stopped",
            "paused" => "status-paused",
            _ => "status-unknown"
        };
        return "<span class=\"" + css + "\">" + Esc(status) + "</span>";
    }

    // -------------------------------------------------------------------------
    // Shared formatting helpers
    // -------------------------------------------------------------------------

    private static string FormatConfidence(ConfidenceDto c) =>
        Esc(c.Value.ToString("0.00", CultureInfo.InvariantCulture)) + " (" + Esc(c.Band) + ")";

    private static string EvidenceList(IReadOnlyList<EvidenceDto> evidence)
    {
        if (evidence.Count == 0) return "<span class=\"empty\">none</span>";
        var sb = new StringBuilder("<ul class=\"evidence-list\">\n");
        foreach (var e in evidence)
        {
            sb.Append("<li><code>").Append(Esc(e.Type)).Append("</code> ").Append(Esc(e.Location));
            if (!string.IsNullOrEmpty(e.Detail))
                sb.Append(" &mdash; ").Append(Esc(e.Detail));
            sb.Append("</li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string IdTags(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0) return string.Empty;
        var sb = new StringBuilder("<ul class=\"tags\">\n");
        foreach (var id in ids)
            sb.Append("<li>").Append(Esc(id)).Append("</li>\n");
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string StringListToTags(IReadOnlyList<string> values)
    {
        if (values.Count == 0) return "<span class=\"empty\">none</span>";
        var sb = new StringBuilder("<ul class=\"tags\">\n");
        foreach (var v in values)
            sb.Append("<li>").Append(Esc(v)).Append("</li>\n");
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string Badge(string prefix, string value) =>
        "<span class=\"badge " + prefix + "-" + CssClassName.From(value) + "\">" + Esc(value) + "</span>";

    private string L(string key) => ReportLabels.Get(key, _language);

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static string Esc(string? value) =>
        string.IsNullOrEmpty(value) ? "&mdash;" : Encoder.Encode(value);
}
