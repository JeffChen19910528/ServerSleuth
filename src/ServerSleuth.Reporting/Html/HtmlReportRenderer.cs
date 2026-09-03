using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Reporting.Json.Dto;

namespace ServerSleuth.Reporting.Html;

/// <summary>
/// Renders a <see cref="ServerMigrationAssessmentReport"/> as a single, self-contained,
/// offline-capable HTML "Server Deployment Inventory" document. The report answers exactly one
/// question — "what is deployed, installed, and running on this server?" — and deliberately
/// omits Risk/Migration/Security/Configuration-audit content (severity, blocking issues,
/// verification checks, coverage, confidence, policy decisions). That analysis still runs
/// internally (see <c>ServerSleuth.Analysis</c>) and is still available via the JSON report; it
/// is simply not rendered here.
///
/// Supports <see cref="ReportLanguage.En"/> and <see cref="ReportLanguage.ZhTw"/> (default UI
/// language for this report). Data values (service names, paths, software names) are never
/// translated — only UI labels, headings, and column headers.
///
/// All dynamic text is passed through <see cref="HtmlEncoder"/> with <see cref="UnicodeRanges.All"/>
/// before being written — HTML-required characters are always escaped, while readable Unicode
/// (Traditional Chinese, Latin-1 accents) is preserved as literal characters.
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
        var appRows = BuildApplicationRows();
        var services = dto.Services.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var software = dto.Software.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var serviceCount = services.Count(s => !DeploymentClassifier.IsSystem(s));
        var softwareCount = software.Count(s => !DeploymentClassifier.IsSystem(s));
        var componentGroups = BuildComponentGroups(dto);
        var componentCount = componentGroups.Sum(g => g.Components.Count);
        var tasks = dto.ScheduledTasks.Where(t => !DeploymentClassifier.IsSystem(t))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var sb = new StringBuilder();
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
            sb.Append("<p class=\"muted\">").Append(Esc(L("scan-time"))).Append(' ')
              .Append(Esc(generatedAt.ToString("u", CultureInfo.InvariantCulture)))
              .Append("</p>\n");
        }

        AppendServerInformation(sb);
        AppendSummary(sb, appRows.Count, serviceCount, softwareCount, componentCount);
        AppendApplications(sb, dto, appRows);
        AppendServices(sb, services);
        AppendSoftware(sb, software);
        AppendComponents(sb, componentGroups);
        AppendScheduledTasks(sb, tasks);
        AppendRuntime(sb, dto, appRows);
        AppendDatabases(sb, appRows);

        sb.Append("</main>\n");
        AppendInlineScript(sb);
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Server Information
    // -------------------------------------------------------------------------

    private void AppendServerInformation(StringBuilder sb)
    {
        var server = _discovery?.Entities.OfType<Server>().FirstOrDefault();
        var os = _discovery?.Entities.OfType<Core.Models.OperatingSystem>().FirstOrDefault();

        sb.Append("<section id=\"server-info\">\n<h2>").Append(Esc(L("server-info"))).Append("</h2>\n");
        sb.Append("<div class=\"panel\">\n");
        AppendInfoRow(sb, L("col-server-name"), server?.Hostname ?? server?.Name);
        AppendInfoRow(sb, L("col-operating-system"), os?.Platform ?? os?.Name);
        sb.Append("</div>\n</section>\n");
    }

    private static void AppendInfoRow(StringBuilder sb, string label, string? value)
    {
        sb.Append("<p><span class=\"muted\">").Append(Esc(label)).Append("</span> ")
          .Append(Esc(value)).Append("</p>\n");
    }

    // -------------------------------------------------------------------------
    // Summary
    // -------------------------------------------------------------------------

    private void AppendSummary(StringBuilder sb, int appCount, int serviceCount, int softwareCount, int componentCount)
    {
        sb.Append("<section id=\"summary\">\n<h2>").Append(Esc(L("summary"))).Append("</h2>\n");
        sb.Append("<label class=\"show-system-toggle\"><input type=\"checkbox\" onchange=\"toggleSystem(this)\"> ")
          .Append(Esc(L("show-system-components"))).Append("</label>\n");
        sb.Append("<div class=\"grid\">\n");
        AppendDashboardStat(sb, appCount, L("stat-applications"), "#applications");
        AppendDashboardStat(sb, serviceCount, L("stat-services"), "#windows-services");
        AppendDashboardStat(sb, softwareCount, L("stat-software"), "#installed-software");
        AppendDashboardStat(sb, componentCount, L("stat-application-components"), "#application-components");
        sb.Append("</div>\n</section>\n");
    }

    private static void AppendDashboardStat(StringBuilder sb, int count, string label, string href)
    {
        sb.Append("<a href=\"").Append(href).Append("\" class=\"stat stat-link\">");
        sb.Append("<div class=\"value\">").Append(count).Append("</div>");
        sb.Append("<div class=\"label\">").Append(Esc(label)).Append("</div>");
        sb.Append("</a>\n");
    }

    // -------------------------------------------------------------------------
    // 1. Applications
    // -------------------------------------------------------------------------

    private sealed record AppRow(string Name, string Type, string? Path, string? Status);

    private IReadOnlyList<AppRow> BuildApplicationRows()
    {
        if (_discovery is null || _boundaries is null || _boundaries.Count == 0) return [];

        var entitiesById = _discovery.Entities
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var rows = new List<AppRow>();
        foreach (var boundary in _boundaries)
        {
            var anchor = FindAnchor(boundary, entitiesById, out var type, out var path);
            if (anchor is null) continue;
            if (DeploymentClassifier.IsSystemPath(path)) continue;

            var status = anchor.Status == EntityStatus.Unknown ? null : anchor.Status.ToString();
            rows.Add(new AppRow(boundary.Name, type, path, status));
        }

        return rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private DiscoveryEntity? FindAnchor(
        ApplicationBoundary boundary, IReadOnlyDictionary<string, DiscoveryEntity> entitiesById,
        out string type, out string? path)
    {
        foreach (var memberId in boundary.MemberEntityIds)
        {
            if (entitiesById.TryGetValue(memberId, out var e) && e is Application app)
            {
                type = L("app-type-iis");
                path = app.Path;
                return app;
            }
        }

        foreach (var memberId in boundary.MemberEntityIds)
        {
            if (entitiesById.TryGetValue(memberId, out var e) && e is Service svc &&
                !string.IsNullOrEmpty(svc.ExecutablePath))
            {
                type = L("app-type-service");
                path = svc.ExecutablePath;
                return svc;
            }
        }

        foreach (var memberId in boundary.MemberEntityIds)
        {
            if (entitiesById.TryGetValue(memberId, out var e) && e is ScheduledTask task &&
                !string.IsNullOrEmpty(task.Action))
            {
                type = L("app-type-console");
                path = task.Action;
                return task;
            }
        }

        type = string.Empty;
        path = null;
        return null;
    }

    private void AppendApplications(StringBuilder sb, ServerReportDto dto, IReadOnlyList<AppRow> apps)
    {
        sb.Append("<section id=\"applications\">\n<h2>1. ").Append(Esc(L("applications"))).Append("</h2>\n");
        if (apps.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        foreach (var app in apps)
        {
            sb.Append("<details class=\"panel\" open>\n<summary>").Append(Esc(app.Name)).Append("</summary>\n");
            AppendInfoRow(sb, L("col-type"), app.Type);
            AppendInfoRow(sb, L("col-path"), app.Path);
            AppendInfoRow(sb, L("col-status"), TranslateStatus(app.Status));

            AppendNestedList(sb, L("col-certificate"), dto.Certificates
                .Where(c => c.ApplicationName == app.Name)
                .Select(c => Esc(c.Subject ?? c.Name) + (string.IsNullOrEmpty(c.ValidTo) ? "" : " (" + Esc(L("col-valid-to")) + " " + Esc(c.ValidTo) + ")")));

            AppendNestedList(sb, L("col-configuration-file"), dto.Configurations
                .Where(c => c.ApplicationName == app.Name)
                .Select(c => Esc(c.Name)));

            AppendNestedList(sb, L("col-external-connection"), dto.ExternalConnections
                .Where(c => c.ApplicationName == app.Name)
                .Select(c => Esc(c.Endpoint ?? c.Name)));

            sb.Append("</details>\n");
        }

        sb.Append("</section>\n");
    }

    private static void AppendNestedList(StringBuilder sb, string label, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        sb.Append("<p class=\"muted\">").Append(Esc(label)).Append("</p>\n<ul class=\"tags\">\n");
        foreach (var item in list)
            sb.Append("<li>").Append(item).Append("</li>\n");
        sb.Append("</ul>\n");
    }

    // -------------------------------------------------------------------------
    // 2. Windows Services
    // -------------------------------------------------------------------------

    private void AppendServices(StringBuilder sb, IReadOnlyList<InventoryEntityDto> services)
    {
        sb.Append("<section id=\"windows-services\">\n<h2>2. ").Append(Esc(L("windows-services"))).Append("</h2>\n");
        if (services.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        sb.Append("<table><thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-display-name"));
        AppendTh(sb, L("col-status"));
        AppendTh(sb, L("col-start-type"));
        AppendTh(sb, L("col-account"));
        AppendTh(sb, L("col-path"));
        AppendTh(sb, L("col-publisher"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var svc in services)
        {
            var cls = DeploymentClassifier.IsSystem(svc) ? "system" : "other";
            sb.Append("<tr data-cls=\"").Append(cls).Append("\">");
            sb.Append("<td><strong>").Append(Esc(svc.Name)).Append("</strong></td>");
            sb.Append("<td>").Append(Esc(svc.DisplayName)).Append("</td>");
            sb.Append("<td>").Append(StatusInline(svc.Status)).Append("</td>");
            sb.Append("<td>").Append(Esc(svc.StartType)).Append("</td>");
            sb.Append("<td>").Append(Esc(svc.ServiceAccount)).Append("</td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(svc.ExecutablePath ?? svc.Path)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(svc.Publisher)).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody></table>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // 3. Installed Software
    // -------------------------------------------------------------------------

    private void AppendSoftware(StringBuilder sb, IReadOnlyList<InventoryEntityDto> software)
    {
        sb.Append("<section id=\"installed-software\">\n<h2>3. ").Append(Esc(L("installed-software"))).Append("</h2>\n");
        if (software.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        sb.Append("<table><thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-version"));
        AppendTh(sb, L("col-publisher"));
        AppendTh(sb, L("col-install-date"));
        AppendTh(sb, L("col-path"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var sw in software)
        {
            var cls = DeploymentClassifier.IsSystem(sw) ? "system" : "other";
            sb.Append("<tr data-cls=\"").Append(cls).Append("\">");
            sb.Append("<td><strong>").Append(Esc(sw.Name)).Append("</strong></td>");
            sb.Append("<td>").Append(Esc(sw.Version)).Append("</td>");
            sb.Append("<td>").Append(Esc(sw.Publisher)).Append("</td>");
            sb.Append("<td>").Append(Esc(sw.InstallDate)).Append("</td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(sw.InstallLocation ?? sw.Path)).Append("</span></td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody></table>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // 4. Application Components (DLL / COM, grouped by Application)
    // -------------------------------------------------------------------------

    private sealed record ComponentGroup(string ApplicationName, IReadOnlyList<InventoryEntityDto> Components);

    private static IReadOnlyList<ComponentGroup> BuildComponentGroups(ServerReportDto dto)
    {
        var candidates = dto.DllBinaries.Concat(dto.ComComponents)
            .Where(e => !string.IsNullOrEmpty(e.ApplicationName) && !DeploymentClassifier.IsSystem(e));

        return candidates
            .GroupBy(e => e.ApplicationName!, StringComparer.Ordinal)
            .Select(g => new ComponentGroup(g.Key, g.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(g => g.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AppendComponents(StringBuilder sb, IReadOnlyList<ComponentGroup> groups)
    {
        sb.Append("<section id=\"application-components\">\n<h2>4. ").Append(Esc(L("application-components"))).Append("</h2>\n");
        if (groups.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        foreach (var group in groups)
        {
            sb.Append("<details class=\"panel\">\n<summary>").Append(Esc(group.ApplicationName))
              .Append(" (").Append(group.Components.Count).Append(")</summary>\n");
            sb.Append("<table><thead><tr>");
            AppendTh(sb, L("col-component"));
            AppendTh(sb, L("col-type"));
            AppendTh(sb, L("col-version"));
            AppendTh(sb, L("col-path"));
            sb.Append("</tr></thead>\n<tbody>\n");

            foreach (var c in group.Components)
            {
                var type = c.EntityType == "ComComponent" ? "COM" : "DLL";
                var path = c.InprocServer32 ?? c.Path;
                sb.Append("<tr>");
                sb.Append("<td><strong>").Append(Esc(c.Name)).Append("</strong></td>");
                sb.Append("<td>").Append(Esc(type)).Append("</td>");
                sb.Append("<td>").Append(Esc(c.Version)).Append("</td>");
                sb.Append("<td><span class=\"muted\">").Append(Esc(path)).Append("</span></td>");
                sb.Append("</tr>\n");
            }

            sb.Append("</tbody></table>\n</details>\n");
        }

        sb.Append("</section>\n");
    }

    // -------------------------------------------------------------------------
    // 5. Business Scheduled Tasks
    // -------------------------------------------------------------------------

    private void AppendScheduledTasks(StringBuilder sb, IReadOnlyList<InventoryEntityDto> tasks)
    {
        sb.Append("<section id=\"business-scheduled-tasks\">\n<h2>5. ").Append(Esc(L("business-scheduled-tasks"))).Append("</h2>\n");
        if (tasks.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        sb.Append("<table><thead><tr>");
        AppendTh(sb, L("col-name"));
        AppendTh(sb, L("col-application"));
        AppendTh(sb, L("col-trigger"));
        AppendTh(sb, L("col-run-as"));
        AppendTh(sb, L("col-action"));
        AppendTh(sb, L("col-status"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var task in tasks)
        {
            var enabled = string.Equals(task.Enabled, "True", StringComparison.OrdinalIgnoreCase);
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(task.Name)).Append("</strong></td>");
            sb.Append("<td>").Append(Esc(task.ApplicationName)).Append("</td>");
            sb.Append("<td>").Append(Esc(task.Trigger)).Append("</td>");
            sb.Append("<td>").Append(Esc(task.RunAsAccount)).Append("</td>");
            sb.Append("<td><span class=\"muted\">").Append(Esc(task.TaskAction)).Append("</span></td>");
            sb.Append("<td>").Append(enabled
                ? "<span class=\"status-enabled\">" + Esc(L("status-enabled")) + "</span>"
                : "<span class=\"status-disabled\">" + Esc(L("status-disabled")) + "</span>").Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody></table>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // 6. Application Runtime
    // -------------------------------------------------------------------------

    private void AppendRuntime(StringBuilder sb, ServerReportDto dto, IReadOnlyList<AppRow> apps)
    {
        sb.Append("<section id=\"application-runtime\">\n<h2>6. ").Append(Esc(L("application-runtime"))).Append("</h2>\n");
        if (apps.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        sb.Append("<table><thead><tr>");
        AppendTh(sb, L("col-application"));
        AppendTh(sb, L("col-runtime"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var app in apps)
        {
            var runtimes = dto.Runtimes.Where(r => r.ApplicationName == app.Name)
                .Select(r => string.IsNullOrEmpty(r.Version) ? r.Name : r.Name + " " + r.Version)
                .ToList();
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(app.Name)).Append("</strong></td>");
            sb.Append("<td>").Append(runtimes.Count == 0 ? Esc(L("cls-unknown")) : Esc(string.Join(", ", runtimes))).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody></table>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // 7. Application Database References
    // -------------------------------------------------------------------------

    private void AppendDatabases(StringBuilder sb, IReadOnlyList<AppRow> apps)
    {
        sb.Append("<section id=\"application-databases\">\n<h2>7. ").Append(Esc(L("application-database-refs"))).Append("</h2>\n");

        if (_discovery is null || _boundaries is null || apps.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        var boundariesByName = _boundaries.ToDictionary(b => b.Name, StringComparer.Ordinal);
        var databases = _discovery.Entities.OfType<Database>().ToList();

        var rows = new List<(string App, IReadOnlyList<Database> Databases)>();
        foreach (var app in apps)
        {
            if (!boundariesByName.TryGetValue(app.Name, out var boundary)) continue;
            var memberSet = boundary.MemberEntityIds.ToHashSet(StringComparer.Ordinal);
            var dbs = databases.Where(d => memberSet.Contains(d.Id))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
            if (dbs.Count > 0) rows.Add((app.Name, dbs));
        }

        if (rows.Count == 0)
        {
            sb.Append("<p class=\"empty\">").Append(Esc(L("empty-none"))).Append("</p>\n</section>\n");
            return;
        }

        sb.Append("<table><thead><tr>");
        AppendTh(sb, L("col-application"));
        AppendTh(sb, L("col-database"));
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var (app, dbs) in rows)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(app)).Append("</strong></td>");
            sb.Append("<td>").Append(Esc(string.Join(", ", dbs.Select(d => d.Name)))).Append("</td>");
            sb.Append("</tr>\n");
        }

        sb.Append("</tbody></table>\n</section>\n");
    }

    // -------------------------------------------------------------------------
    // Inline JavaScript (Show System Components toggle)
    // -------------------------------------------------------------------------

    private static void AppendInlineScript(StringBuilder sb)
    {
        sb.Append("<script>\n");
        sb.Append("""
            window.toggleSystem=function(checkbox){
              document.body.classList.toggle('show-system', checkbox.checked);
            };
            """);
        sb.Append("\n</script>\n");
    }

    // -------------------------------------------------------------------------
    // Shared formatting helpers
    // -------------------------------------------------------------------------

    private static void AppendTh(StringBuilder sb, string label) =>
        sb.Append("<th>").Append(Esc(label)).Append("</th>");

    private string StatusInline(string? status)
    {
        if (string.IsNullOrEmpty(status)) return "&mdash;";
        var css = status.ToLowerInvariant() switch
        {
            "running" => "status-running",
            "stopped" => "status-stopped",
            "paused" => "status-paused",
            _ => "status-unknown"
        };
        var label = status.ToLowerInvariant() switch
        {
            "running" => L("status-running"),
            "stopped" => L("status-stopped"),
            "installed" => L("status-installed"),
            "configured" => L("status-configured"),
            _ => status
        };
        return "<span class=\"" + css + "\">" + Esc(label) + "</span>";
    }

    private string? TranslateStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "running" => L("status-running"),
        "stopped" => L("status-stopped"),
        "installed" => L("status-installed"),
        "configured" => L("status-configured"),
        _ => status
    };

    private string L(string key) => ReportLabels.Get(key, _language);

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static string Esc(string? value) =>
        string.IsNullOrEmpty(value) ? "&mdash;" : Encoder.Encode(value);
}
