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
/// offline-capable HTML document — see skill.md (Phase 9B) §1-3. Reuses Phase 9A's
/// <see cref="ReportDtoMapper"/>/DTO tree rather than re-reading the domain model or duplicating
/// its own mapping — the exact same secret-safety/raw-configuration-safety boundary that tree
/// already establishes for JSON applies here unchanged (§16-17): this renderer never touches
/// <see cref="ServerMigrationAssessmentReport"/>'s underlying domain objects directly, only the
/// already-mapped DTOs.
///
/// Never recalculates status/severity/policy/dependency/action/check/coverage semantics (§1) —
/// every value rendered is read straight off the DTO tree. Every piece of dynamic text is passed
/// through <see cref="WebUtility.HtmlEncode"/> before being written (§18); nothing is ever
/// interpolated into the document unescaped. No <c>&lt;script&gt;</c> tag is emitted anywhere —
/// collapsible sections use native <c>&lt;details&gt;</c>/<c>&lt;summary&gt;</c>, which need no
/// JavaScript at all (§20), so the report is captured/exercised identically with or without a
/// scripting-capable viewer.
/// </summary>
public sealed class HtmlReportRenderer : IReportRenderer
{
    private readonly DateTimeOffset? _generatedAt;
    private readonly AggregateDiscoveryResult? _discovery;
    private readonly IReadOnlyList<ApplicationBoundary>? _boundaries;
    private readonly IReadOnlyList<ExternalDependency>? _externalDependencies;

    /// <summary>
    /// <paramref name="generatedAt"/> is opt-in and <c>null</c> by default (§22). The optional
    /// inventory parameters (<paramref name="discovery"/>, <paramref name="boundaries"/>,
    /// <paramref name="externalDependencies"/>) are GUI-8C additions — when supplied the HTML
    /// gains nine inventory sections (DLL, Runtime, Service, COM, Software, Task, Certificate,
    /// Config, External) positioned before Risk/Migration sections. Omitting them keeps the
    /// existing output byte-identical (backward compatible).
    /// </summary>
    public HtmlReportRenderer(
        DateTimeOffset? generatedAt = null,
        AggregateDiscoveryResult? discovery = null,
        IReadOnlyList<ApplicationBoundary>? boundaries = null,
        IReadOnlyList<ExternalDependency>? externalDependencies = null)
    {
        _generatedAt = generatedAt;
        _discovery = discovery;
        _boundaries = boundaries;
        _externalDependencies = externalDependencies;
    }

    public ReportFormat Format => ReportFormat.Html;

    public ReportRenderResult Render(ServerMigrationAssessmentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var dto = _discovery is not null && _boundaries is not null && _externalDependencies is not null
            ? ReportDtoMapper.ToDto(report, _discovery, _boundaries, _externalDependencies)
            : ReportDtoMapper.ToDto(report);

        var html = BuildDocument(dto);

        return new ReportRenderResult
        {
            Format = ReportFormat.Html,
            Content = html,
            Encoding = Encoding.UTF8
        };
    }

    private string BuildDocument(ServerReportDto dto)
    {
        var sb = new StringBuilder();

        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(Esc("ServerSleuth Migration Assessment Report")).Append("</title>\n");
        sb.Append("<style>\n").Append(HtmlDocumentStyles.Css).Append("\n</style>\n");
        sb.Append("</head>\n<body>\n<main>\n");

        sb.Append("<h1>Server Migration Assessment Report</h1>\n");
        if (_generatedAt is { } generatedAt)
        {
            sb.Append("<p class=\"muted\">Generated: ").Append(Esc(generatedAt.ToString("u", CultureInfo.InvariantCulture))).Append("</p>\n");
        }

        AppendExecutiveSummary(sb, dto);
        AppendCoverage(sb, dto);
        AppendApplications(sb, dto.Applications);

        // GUI-8C: inventory sections — appear before risk/migration so the report answers
        // "what must I prepare?" before "what is blocked?". Each section is skipped when empty.
        AppendInventorySection(sb, "dll-binaries", "DLL / Binary", dto.DllBinaries);
        AppendInventorySection(sb, "windows-services", "Windows Services", dto.Services);
        AppendInventorySection(sb, "com-components", "COM Components", dto.ComComponents);
        AppendInventorySection(sb, "installed-software", "Installed Software", dto.Software);
        AppendInventorySection(sb, "runtime-requirements", "Runtime Requirements", dto.Runtimes);
        AppendInventorySection(sb, "scheduled-tasks", "Scheduled Tasks", dto.ScheduledTasks);
        AppendInventorySection(sb, "certificates", "Certificates", dto.Certificates);
        AppendInventorySection(sb, "configuration-files", "Configuration Files", dto.Configurations);
        AppendInventorySection(sb, "external-connections", "External Connections", dto.ExternalConnections);

        AppendMigrationChecklist(sb, dto);

        AppendActions(sb, dto.Actions);
        AppendChecks(sb, "Pre-Migration Verification Checks", dto.PreMigrationChecks);
        AppendChecks(sb, "Post-Migration Verification Checks", dto.PostMigrationChecks);
        AppendIssueList(sb, "Server-Level Issues", dto.ServerLevelIssues);
        AppendSharedInfrastructure(sb, dto.SharedInfrastructure);
        AppendDependencyGroups(sb, dto.Dependencies);
        AppendGraphValidationErrors(sb, dto.GraphValidationErrors);
        AppendDiagnostics(sb, dto.Diagnostics);

        sb.Append("</main>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static void AppendInventorySection(
        StringBuilder sb, string id, string title, IReadOnlyList<InventoryEntityDto> entities)
    {
        if (entities.Count == 0)
        {
            return;
        }

        sb.Append("<section id=\"").Append(id).Append("\">\n");
        sb.Append("<h2>").Append(Esc(title)).Append(" (").Append(entities.Count).Append(")</h2>\n");
        sb.Append("<details class=\"panel\">\n<summary>").Append(Esc(title)).Append(" — ")
            .Append(entities.Count).Append(" item").Append(entities.Count == 1 ? string.Empty : "s").Append("</summary>\n");

        sb.Append("<table>\n<thead><tr>");
        sb.Append("<th>Name</th><th>Application</th><th>Version / Details</th><th>Path</th><th>Status</th>");
        sb.Append("</tr></thead>\n<tbody>\n");

        foreach (var e in entities)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(e.Name)).Append("</strong>");
            if (!string.IsNullOrEmpty(e.Architecture))
            {
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Architecture)).Append("</span>");
            }
            if (!string.IsNullOrEmpty(e.DisplayName) && e.DisplayName != e.Name)
            {
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.DisplayName)).Append("</span>");
            }
            if (!string.IsNullOrEmpty(e.Subject))
            {
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Subject)).Append("</span>");
            }
            if (!string.IsNullOrEmpty(e.Clsid))
            {
                sb.Append("<br><code>").Append(Esc(e.Clsid)).Append("</code>");
            }
            if (!string.IsNullOrEmpty(e.Kind))
            {
                sb.Append("<br><span class=\"muted\">").Append(Esc(e.Kind)).Append("</span>");
            }
            sb.Append("</td>");

            sb.Append("<td>").Append(Esc(e.ApplicationName)).Append("</td>");

            sb.Append("<td>");
            if (!string.IsNullOrEmpty(e.Version)) { sb.Append(Esc(e.Version)); }
            if (!string.IsNullOrEmpty(e.StartType)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.StartType)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.ServiceAccount)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.ServiceAccount)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.ThreadingModel)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.ThreadingModel)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.ValidTo)) { sb.Append("<br><span class=\"muted\">Valid to: ").Append(Esc(e.ValidTo)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.Folder)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.Folder)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.RunAsAccount)) { sb.Append("<br><span class=\"muted\">Run as: ").Append(Esc(e.RunAsAccount)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.Format)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.Format)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.Publisher)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.Publisher)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.InstallDate)) { sb.Append("<br><span class=\"muted\">").Append(Esc(e.InstallDate)).Append("</span>"); }
            if (!string.IsNullOrEmpty(e.Endpoint)) { sb.Append("<br><code>").Append(Esc(e.Endpoint)).Append("</code>"); }
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

    /// <summary>
    /// GUI-8C §12, vocabulary centralized by GUI-9B §4 — a dedicated "Migration Checklist"
    /// section summarizing what must be prepared per discovered category. The action text for
    /// each row now comes from <see cref="MigrationIntentCatalog"/> (the single source of truth
    /// for category → <see cref="MigrationIntent"/>, keyed by the same
    /// <see cref="InventoryEntityDto.EntityType"/> strings <see cref="ReportDtoMapper"/> assigns)
    /// instead of a locally hard-coded string per category — this is a behavior-preserving
    /// refactor of where the vocabulary lives, not a report redesign (skill.md GUI-9B §4, §16).
    /// This is a summary of the per-item inventory tables above; it performs no calculation and
    /// fabricates nothing — counts are the same <see cref="InventoryEntityDto"/> lists already
    /// rendered. A category with zero discovered items is omitted entirely (§四: never fabricate
    /// an item for an absent category).
    /// </summary>
    private static void AppendMigrationChecklist(StringBuilder sb, ServerReportDto dto)
    {
        static string Action(string category) => string.Join(" / ", MigrationIntentCatalog.IntentsFor(category));

        var rows = new (string Category, int Count, string Action)[]
        {
            ("Application Components (DLL / Binary)", dto.DllBinaries.Count, Action("Dll")),
            ("Runtime Requirements", dto.Runtimes.Count, Action("Runtime")),
            ("Windows Services", dto.Services.Count, Action("Service")),
            ("COM Components", dto.ComComponents.Count, Action("ComComponent")),
            ("Installed Software", dto.Software.Count, Action("Software")),
            ("Scheduled Tasks", dto.ScheduledTasks.Count, Action("ScheduledTask")),
            ("Certificates", dto.Certificates.Count, Action("Certificate")),
            ("Configuration", dto.Configurations.Count, Action("Configuration")),
            ("External Connections", dto.ExternalConnections.Count, Action("ExternalDependency")),
        };

        var applicable = rows.Where(r => r.Count > 0).ToList();
        if (applicable.Count == 0)
        {
            return;
        }

        sb.Append("<section id=\"migration-checklist\">\n<h2>Migration Checklist</h2>\n");
        sb.Append("<p class=\"muted\">What to prepare when moving this server's applications to a new server.</p>\n");
        sb.Append("<table>\n<thead><tr><th>Category</th><th>Discovered</th><th>Migration Action</th></tr></thead>\n<tbody>\n");
        foreach (var row in applicable)
        {
            sb.Append("<tr><td>").Append(Esc(row.Category)).Append("</td><td>").Append(row.Count)
                .Append("</td><td>").Append(Esc(row.Action)).Append("</td></tr>\n");
        }
        sb.Append("</tbody>\n</table>\n</section>\n");
    }

    private static void AppendExecutiveSummary(StringBuilder sb, ServerReportDto dto)
    {
        var s = dto.Server;
        sb.Append("<section id=\"executive-summary\">\n<h2>Executive Summary</h2>\n<div class=\"panel\">\n");
        sb.Append("<p>").Append(Badge("status", s.OverallMigrationStatus)).Append(' ').Append(Badge("severity", s.OverallRiskSeverity)).Append("</p>\n");
        sb.Append("<div class=\"grid\">\n");
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
        sb.Append("</div>\n</div>\n</section>\n");
    }

    private static void AppendStat(StringBuilder sb, string label, int value)
    {
        sb.Append("<div class=\"stat\"><div class=\"value\">").Append(value).Append("</div><div class=\"label\">")
            .Append(Esc(label)).Append("</div></div>\n");
    }

    private static void AppendCoverage(StringBuilder sb, ServerReportDto dto)
    {
        sb.Append("<section id=\"coverage\">\n<h2>Assessment Coverage</h2>\n<div class=\"panel\">\n");
        sb.Append("<p>").Append(Badge("coverage", dto.Coverage)).Append("</p>\n");
        sb.Append("<p class=\"muted\">Coverage is independent of Migration Status — a Ready server may still have Partial or Limited coverage.</p>\n");

        if (dto.CoverageWarnings.Count == 0)
        {
            sb.Append("<p class=\"empty\">No coverage warnings.</p>\n");
        }
        else
        {
            sb.Append("<table>\n<thead><tr><th>Scanner</th><th>Status</th><th>Platform</th><th>Reason</th><th>Evidence</th></tr></thead>\n<tbody>\n");
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

    private static void AppendApplications(StringBuilder sb, IReadOnlyList<ApplicationDto> applications)
    {
        sb.Append("<section id=\"applications\">\n<h2>Applications</h2>\n");
        if (applications.Count == 0)
        {
            sb.Append("<p class=\"empty\">No application boundaries with attributed findings.</p>\n</section>\n");
            return;
        }

        foreach (var app in applications)
        {
            sb.Append("<details class=\"panel\" open>\n<summary>").Append(Esc(app.ApplicationName))
                .Append(" (<code>").Append(Esc(app.BoundaryId)).Append("</code>) ")
                .Append(Badge("status", app.MigrationStatus)).Append(' ').Append(Badge("severity", app.RiskSeverity))
                .Append("</summary>\n");

            sb.Append("<p class=\"muted\">Affected entities: ").Append(app.AffectedEntityCount)
                .Append(" &middot; Affected boundaries: ").Append(app.AffectedBoundaryCount).Append("</p>\n");

            AppendIssueTable(sb, app.Issues);
            AppendDependencyTable(sb, app.Dependencies);
            AppendActionTable(sb, app.Actions);
            AppendCheckTable(sb, "Pre-Migration Checks", app.PreMigrationChecks);
            AppendCheckTable(sb, "Post-Migration Checks", app.PostMigrationChecks);

            sb.Append("</details>\n");
        }

        sb.Append("</section>\n");
    }

    private static void AppendIssueList(StringBuilder sb, string title, IReadOnlyList<IssueDto> issues)
    {
        sb.Append("<section>\n<h2>").Append(Esc(title)).Append("</h2>\n");
        if (issues.Count == 0)
        {
            sb.Append("<p class=\"empty\">None.</p>\n</section>\n");
            return;
        }

        AppendIssueTable(sb, issues);
        sb.Append("</section>\n");
    }

    private static void AppendIssueTable(StringBuilder sb, IReadOnlyList<IssueDto> issues)
    {
        if (issues.Count == 0)
        {
            return;
        }

        sb.Append("<h3>Issues</h3>\n<table>\n<thead><tr><th>Issue</th><th>Rule</th><th>Severity</th><th>Impact</th><th>Confidence</th><th>Affected</th><th>Evidence</th></tr></thead>\n<tbody>\n");
        foreach (var i in issues)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(i.Title)).Append("</strong><br><span class=\"muted\">").Append(Esc(i.Description)).Append("</span>")
                .Append("<br><code>").Append(Esc(i.IssueId)).Append("</code></td>");
            sb.Append("<td><code>").Append(Esc(i.RuleId)).Append("</code><br><span class=\"muted\">").Append(Esc(i.SourceRiskFindingId)).Append("</span></td>");
            sb.Append("<td>").Append(Badge("severity", i.Severity)).Append("</td>");
            sb.Append("<td>").Append(Badge("impact", i.MigrationStatusImpact)).Append("</td>");
            sb.Append("<td>").Append(FormatConfidence(i.Confidence)).Append("</td>");
            sb.Append("<td>").Append(IdTags(i.AffectedBoundaryIds)).Append(IdTags(i.AffectedEntityIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(i.Evidence)).Append("</td>");
            sb.Append("</tr>\n");

            sb.Append("<tr><td colspan=\"7\"><span class=\"muted\">Required action:</span> ").Append(Esc(i.RequiredAction))
                .Append("<br><span class=\"muted\">Policy decision:</span> ").Append(Esc(i.PolicyDecisionReason)).Append("</td></tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    private static void AppendSharedInfrastructure(StringBuilder sb, IReadOnlyList<DependencyDto> shared)
    {
        sb.Append("<section id=\"shared-infrastructure\">\n<h2>Shared Infrastructure</h2>\n");
        if (shared.Count == 0)
        {
            sb.Append("<p class=\"empty\">No dependency is shared across more than one application boundary.</p>\n</section>\n");
            return;
        }

        sb.Append("<p class=\"muted\">Each row below is exactly one logical dependency shared by every boundary listed — never duplicated per boundary.</p>\n");
        AppendDependencyTable(sb, shared);
        sb.Append("</section>\n");
    }

    private static void AppendDependencyGroups(StringBuilder sb, IReadOnlyList<DependencyGroupDto> groups)
    {
        sb.Append("<section id=\"dependencies\">\n<h2>Dependencies</h2>\n");
        if (groups.Count == 0)
        {
            sb.Append("<p class=\"empty\">No dependencies identified.</p>\n</section>\n");
            return;
        }

        foreach (var group in groups)
        {
            sb.Append("<h3>").Append(Esc(group.Type)).Append(" (").Append(group.Dependencies.Count).Append(")</h3>\n");
            AppendDependencyTable(sb, group.Dependencies);
        }

        sb.Append("</section>\n");
    }

    private static void AppendDependencyTable(StringBuilder sb, IReadOnlyList<DependencyDto> dependencies)
    {
        if (dependencies.Count == 0)
        {
            return;
        }

        sb.Append("<h3>Dependencies</h3>\n<table>\n<thead><tr><th>Dependency</th><th>Type</th><th>Target</th><th>Phase</th><th>Confidence</th><th>Affected Boundaries</th><th>Evidence</th></tr></thead>\n<tbody>\n");
        foreach (var d in dependencies)
        {
            sb.Append("<tr>");
            sb.Append("<td><code>").Append(Esc(d.DependencyId)).Append("</code>")
                .Append(d.RelatedRiskFindingId is null ? string.Empty : "<br><span class=\"muted\">Related: " + Esc(d.RelatedRiskFindingId) + "</span>").Append("</td>");
            sb.Append("<td>").Append(Esc(d.Type)).Append("</td>");
            sb.Append("<td>").Append(Esc(d.Target)).Append("<br><span class=\"muted\">").Append(Esc(d.VerificationRequirement)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(d.VerificationPhase)).Append("</td>");
            sb.Append("<td>").Append(FormatConfidence(d.Confidence)).Append("</td>");
            sb.Append("<td>").Append(IdTags(d.AffectedBoundaryIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(d.Evidence)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    /// <summary>
    /// Grouping is derived purely from the existing, already-structured <c>ActionType</c>/
    /// <c>Phase</c> fields (skill.md §13: "Do not infer action type from text") — an ActionType
    /// beginning with "Review" or "Document" goes in the Review/Documentation bucket; everything
    /// else is grouped by its own <c>Phase</c> value.
    /// </summary>
    private static void AppendActions(StringBuilder sb, IReadOnlyList<ActionDto> actions)
    {
        sb.Append("<section id=\"actions\">\n<h2>Migration Actions</h2>\n");
        if (actions.Count == 0)
        {
            sb.Append("<p class=\"empty\">No actions required.</p>\n</section>\n");
            return;
        }

        var review = actions.Where(a => a.ActionType.StartsWith("Review", StringComparison.Ordinal) || a.ActionType.StartsWith("Document", StringComparison.Ordinal)).ToList();
        var reviewActionIds = review.Select(a => a.ActionId).ToHashSet(StringComparer.Ordinal);
        var pre = actions.Where(a => !reviewActionIds.Contains(a.ActionId) && a.Phase == "PreMigration").ToList();
        var post = actions.Where(a => !reviewActionIds.Contains(a.ActionId) && a.Phase == "PostMigration").ToList();

        AppendActionSubsection(sb, "Pre-Migration", pre);
        AppendActionSubsection(sb, "Post-Migration", post);
        AppendActionSubsection(sb, "Review / Documentation", review);

        sb.Append("</section>\n");
    }

    private static void AppendActionSubsection(StringBuilder sb, string title, IReadOnlyList<ActionDto> actions)
    {
        sb.Append("<h3>").Append(Esc(title)).Append(" (").Append(actions.Count).Append(")</h3>\n");
        AppendActionTable(sb, actions);
    }

    private static void AppendActionTable(StringBuilder sb, IReadOnlyList<ActionDto> actions)
    {
        if (actions.Count == 0)
        {
            sb.Append("<p class=\"empty\">None.</p>\n");
            return;
        }

        sb.Append("<table>\n<thead><tr><th>Action</th><th>Type</th><th>Priority</th><th>Phase</th><th>Related</th><th>Evidence</th></tr></thead>\n<tbody>\n");
        foreach (var a in actions)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(a.Title)).Append("</strong><br><span class=\"muted\">").Append(Esc(a.Description)).Append("</span>")
                .Append("<br><code>").Append(Esc(a.ActionId)).Append("</code>")
                .Append("<br><span class=\"muted\">").Append(Esc(a.Rationale)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(a.ActionType)).Append("</td>");
            sb.Append("<td>").Append(Badge("priority", a.Priority)).Append("</td>");
            sb.Append("<td>").Append(Esc(a.Phase)).Append("</td>");
            sb.Append("<td>").Append(IdTags(a.AffectedBoundaryIds)).Append(IdTags(a.RelatedIssueIds)).Append(IdTags(a.RelatedDependencyIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(a.Evidence)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    private static void AppendChecks(StringBuilder sb, string title, IReadOnlyList<CheckDto> checks)
    {
        sb.Append("<section>\n<h2>").Append(Esc(title)).Append("</h2>\n");
        AppendCheckTable(sb, null, checks);
        sb.Append("</section>\n");
    }

    private static void AppendCheckTable(StringBuilder sb, string? heading, IReadOnlyList<CheckDto> checks)
    {
        if (heading is not null)
        {
            sb.Append("<h3>").Append(Esc(heading)).Append(" (").Append(checks.Count).Append(")</h3>\n");
        }

        if (checks.Count == 0)
        {
            sb.Append("<p class=\"empty\">None.</p>\n");
            return;
        }

        sb.Append("<table>\n<thead><tr><th>Check</th><th>Type</th><th>Phase</th><th>Related</th><th>Evidence</th></tr></thead>\n<tbody>\n");
        foreach (var c in checks)
        {
            sb.Append("<tr>");
            sb.Append("<td><strong>").Append(Esc(c.Title)).Append("</strong><br><span class=\"muted\">").Append(Esc(c.Description)).Append("</span>")
                .Append("<br><code>").Append(Esc(c.CheckId)).Append("</code>")
                .Append("<br><span class=\"muted\">").Append(Esc(c.Rationale)).Append("</span></td>");
            sb.Append("<td>").Append(Esc(c.CheckType)).Append("</td>");
            sb.Append("<td>").Append(Esc(c.Phase)).Append("</td>");
            sb.Append("<td>").Append(IdTags(c.AffectedBoundaryIds)).Append(IdTags(c.RelatedActionIds)).Append(IdTags(c.RelatedDependencyIds)).Append("</td>");
            sb.Append("<td>").Append(EvidenceList(c.Evidence)).Append("</td>");
            sb.Append("</tr>\n");
        }
        sb.Append("</tbody>\n</table>\n");
    }

    private static void AppendGraphValidationErrors(StringBuilder sb, IReadOnlyList<GraphValidationFindingDto> findings)
    {
        sb.Append("<section id=\"graph-validation\">\n<h2>Graph Validation Errors</h2>\n");
        if (findings.Count == 0)
        {
            sb.Append("<p class=\"empty\">No structural graph-integrity errors.</p>\n</section>\n");
            return;
        }

        sb.Append("<table>\n<thead><tr><th>Category</th><th>Code</th><th>Severity</th><th>Message</th><th>Entities</th></tr></thead>\n<tbody>\n");
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

    private static void AppendDiagnostics(StringBuilder sb, DiagnosticsDto d)
    {
        sb.Append("<section id=\"diagnostics\">\n<h2>Diagnostics</h2>\n<div class=\"panel grid\">\n");
        AppendStat(sb, "Applications Consolidated", d.ApplicationsConsolidated);
        AppendStat(sb, "Server-Level Issues", d.ServerLevelIssueCount);
        AppendStat(sb, "Shared Infrastructure Dependencies", d.SharedInfrastructureDependencyCount);
        AppendStat(sb, "Coverage Warnings", d.CoverageWarningCount);
        AppendStat(sb, "Graph Validation Errors", d.GraphValidationErrorCount);
        sb.Append("</div>\n</section>\n");
    }

    private static string FormatConfidence(ConfidenceDto c) =>
        Esc(c.Value.ToString("0.00", CultureInfo.InvariantCulture)) + " (" + Esc(c.Band) + ")";

    private static string EvidenceList(IReadOnlyList<EvidenceDto> evidence)
    {
        if (evidence.Count == 0)
        {
            return "<span class=\"empty\">none</span>";
        }

        var sb = new StringBuilder("<ul class=\"evidence-list\">\n");
        foreach (var e in evidence)
        {
            sb.Append("<li><code>").Append(Esc(e.Type)).Append("</code> ").Append(Esc(e.Location));
            if (!string.IsNullOrEmpty(e.Detail))
            {
                sb.Append(" &mdash; ").Append(Esc(e.Detail));
            }
            sb.Append("</li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string IdTags(IReadOnlyList<string> ids)
    {
        if (ids.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<ul class=\"tags\">\n");
        foreach (var id in ids)
        {
            sb.Append("<li>").Append(Esc(id)).Append("</li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string StringListToTags(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "<span class=\"empty\">none</span>";
        }

        var sb = new StringBuilder("<ul class=\"tags\">\n");
        foreach (var v in values)
        {
            sb.Append("<li>").Append(Esc(v)).Append("</li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string Badge(string prefix, string value) =>
        "<span class=\"badge " + prefix + "-" + CssClassName.From(value) + "\">" + Esc(value) + "</span>";

    /// <summary>
    /// <see cref="HtmlEncoder"/> configured with <see cref="UnicodeRanges.All"/> — HTML-required
    /// characters (&lt; &gt; &amp; &quot; &#39;) are always escaped by <c>HtmlEncoder</c>
    /// regardless of range settings (§18); allowing all Unicode ranges only stops it from ALSO
    /// escaping non-ASCII text (e.g. Traditional Chinese, or Latin-1 Supplement accented
    /// characters `WebUtility.HtmlEncode` would otherwise turn into numeric character
    /// references) into less-readable numeric entities — mirrors the JSON renderer's own
    /// <c>JavaScriptEncoder.Create(UnicodeRanges.All)</c> choice for the same reason (§14).
    /// </summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static string Esc(string? value) => string.IsNullOrEmpty(value) ? "&mdash;" : Encoder.Encode(value);
}
