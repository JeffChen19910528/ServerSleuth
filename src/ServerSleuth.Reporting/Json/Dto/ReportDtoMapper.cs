using System.Globalization;
using ServerSleuth.Analysis.Correlation.Validation;
using ServerSleuth.Analysis.Migration.Actions;
using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Migration.Preparation;
using ServerSleuth.Analysis.Migration.Verification;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>
/// Pure, side-effect-free mapping from Phase 8C's <see cref="ServerMigrationAssessmentReport"/>
/// to the JSON contract's <see cref="ServerReportDto"/> tree — see skill.md (Phase 9A) §1, §4.
/// Every method here is a 1:1 field copy/reshape; none of them recompute a status, severity,
/// action, dependency, or check. Collections are mapped with a plain <c>Select().ToList()</c>
/// over the source's already-deterministic ordering — never grouped/sorted/deduplicated again
/// here, and never iterated via a <c>Dictionary</c>/<c>HashSet</c> (skill.md §8).
/// </summary>
internal static class ReportDtoMapper
{
    public static ServerReportDto ToDto(ServerMigrationAssessmentReport report) => new()
    {
        Server = ToDto(report.ServerSummary),
        Coverage = report.Coverage.ToString(),
        CoverageWarnings = report.CoverageWarnings.Select(ToDto).ToList(),
        Applications = report.ApplicationAssessments.Select(ToDto).ToList(),
        ServerLevelIssues = report.ServerLevelIssues.Select(ToDto).ToList(),
        SharedInfrastructure = report.SharedInfrastructure.Select(ToDto).ToList(),
        Dependencies = report.Dependencies.Select(ToDto).ToList(),
        Actions = report.Actions.Select(ToDto).ToList(),
        PreMigrationChecks = report.PreMigrationChecks.Select(ToDto).ToList(),
        PostMigrationChecks = report.PostMigrationChecks.Select(ToDto).ToList(),
        GraphValidationErrors = report.GraphValidationErrors.Select(ToDto).ToList(),
        Diagnostics = ToDto(report.Diagnostics)
    };

    /// <summary>
    /// GUI-8C overload — maps inventory entities from <paramref name="discovery"/> and
    /// <paramref name="externalDeps"/> into the new inventory list fields on
    /// <see cref="ServerReportDto"/>. The <paramref name="boundaries"/> list is used to build
    /// an entity-id → application-name lookup for attribution; entities not claimed by any
    /// boundary render with <c>ApplicationName = null</c>. Sorting is Name (OrdinalIgnoreCase)
    /// then Id (Ordinal) — identical to <c>ApplicationComponentsViewModel</c>'s own ordering
    /// so the two views of the same data are always consistent.
    /// </summary>
    internal static ServerReportDto ToDto(
        ServerMigrationAssessmentReport report,
        AggregateDiscoveryResult discovery,
        IReadOnlyList<ApplicationBoundary> boundaries,
        IReadOnlyList<ExternalDependency> externalDeps)
    {
        var base64Dto = ToDto(report);

        // Build entity-id → every claiming application-boundary-name lookup, in first-encountered
        // order, from boundary membership. Iterating the list (not a dictionary) for determinism;
        // the inner scan is O(N·M) but discovery sets are small enough that this is never a
        // bottleneck. GUI-9B: names[0] preserves the exact GUI-8C/9A "first boundary wins"
        // ApplicationName value unchanged; the full (sorted) list is the new ApplicationNames.
        var appNamesByEntityId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var boundary in boundaries)
        {
            foreach (var memberId in boundary.MemberEntityIds)
            {
                if (!appNamesByEntityId.TryGetValue(memberId, out var names))
                {
                    appNamesByEntityId[memberId] = names = [];
                }

                if (!names.Contains(boundary.Name, StringComparer.Ordinal))
                {
                    names.Add(boundary.Name);
                }
            }
        }

        string? AppName(string entityId) =>
            appNamesByEntityId.TryGetValue(entityId, out var names) && names.Count > 0 ? names[0] : null;

        IReadOnlyList<string> AppNames(string entityId) =>
            appNamesByEntityId.TryGetValue(entityId, out var names)
                ? names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
                : [];

        InventoryEntityDto BaseDto(DiscoveryEntity e, string entityType) => new()
        {
            Id = e.Id,
            Name = e.Name,
            EntityType = entityType,
            Version = e.Version,
            Architecture = e.Architecture == Core.Enums.EntityArchitecture.Unknown ? null : e.Architecture.ToString(),
            Path = e.Path,
            Status = e.Status == Core.Enums.EntityStatus.Unknown ? null : e.Status.ToString(),
            Publisher = e.Publisher,
            ApplicationName = AppName(e.Id),
            ApplicationNames = AppNames(e.Id)
        };

        var entities = discovery.Entities;

        var inventoryDto = base64Dto with
        {
            DllBinaries = entities.OfType<Dll>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "Dll")).ToList(),

            Runtimes = entities.OfType<Runtime>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "Runtime")).ToList(),

            Services = entities.OfType<Service>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "Service") with
                {
                    DisplayName = e.DisplayName,
                    StartType = e.StartType,
                    ServiceAccount = e.ServiceAccount,
                    ExecutablePath = e.ExecutablePath
                }).ToList(),

            ComComponents = entities.OfType<ComComponent>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "ComComponent") with
                {
                    Clsid = e.Clsid,
                    ProgId = e.ProgId,
                    InprocServer32 = e.InprocServer32,
                    ThreadingModel = e.ThreadingModel
                }).ToList(),

            Software = entities.OfType<Software>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "Software") with
                {
                    InstallLocation = e.InstallLocation,
                    InstallDate = e.InstallDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }).ToList(),

            ScheduledTasks = entities.OfType<ScheduledTask>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "ScheduledTask") with
                {
                    Folder = e.Folder,
                    Trigger = e.Trigger,
                    TaskAction = e.Action,
                    RunAsAccount = e.RunAsAccount,
                    Enabled = e.Enabled.ToString()
                }).ToList(),

            Certificates = entities.OfType<Certificate>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "Certificate") with
                {
                    Subject = e.Subject,
                    Issuer = e.Issuer,
                    Thumbprint = e.Thumbprint,
                    ValidFrom = e.ValidFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ValidTo = e.ValidTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }).ToList(),

            Configurations = entities.OfType<Configuration>()
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => BaseDto(e, "Configuration") with
                {
                    Format = e.Format
                }).ToList(),

            ExternalConnections = externalDeps
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Id, StringComparer.Ordinal)
                .Select(e => new InventoryEntityDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    EntityType = "ExternalDependency",
                    Status = e.Status == Core.Enums.EntityStatus.Unknown ? null : e.Status.ToString(),
                    ApplicationName = AppName(e.Id),
                    ApplicationNames = AppNames(e.Id),
                    Kind = e.Kind,
                    Endpoint = e.Endpoint
                }).ToList()
        };

        // GUI-9B/GUI-10: computed only from the inventory lists just built above (plus
        // Applications' own Count) — never from report.ServerLevelIssues/Actions/
        // PreMigrationChecks or any other Risk/Assessment field (skill.md GUI-9B §1, §7, §11).
        var categoryCounts = new (string Category, int Count)[]
        {
            (MigrationIntentCatalog.ApplicationCategory, inventoryDto.Applications.Count),
            ("Dll", inventoryDto.DllBinaries.Count),
            ("Runtime", inventoryDto.Runtimes.Count),
            ("Service", inventoryDto.Services.Count),
            ("ComComponent", inventoryDto.ComComponents.Count),
            ("Software", inventoryDto.Software.Count),
            ("ScheduledTask", inventoryDto.ScheduledTasks.Count),
            ("Certificate", inventoryDto.Certificates.Count),
            ("Configuration", inventoryDto.Configurations.Count),
            ("ExternalDependency", inventoryDto.ExternalConnections.Count)
        };

        return inventoryDto with { MigrationPreparation = MigrationPreparationSummaryBuilder.Build(categoryCounts) };
    }

    private static ServerSummaryDto ToDto(ServerMigrationSummary s) => new()
    {
        OverallMigrationStatus = s.OverallMigrationStatus.ToString(),
        OverallRiskSeverity = s.OverallRiskSeverity.ToString(),
        ApplicationCount = s.ApplicationCount,
        BlockedApplicationCount = s.BlockedApplicationCount,
        NeedsRemediationApplicationCount = s.NeedsRemediationApplicationCount,
        ReadyWithConditionsApplicationCount = s.ReadyWithConditionsApplicationCount,
        ReadyApplicationCount = s.ReadyApplicationCount,
        BlockingIssueCount = s.BlockingIssueCount,
        RemediationIssueCount = s.RemediationIssueCount,
        ConditionalDependencyCount = s.ConditionalDependencyCount,
        ActionCount = s.ActionCount,
        VerificationCheckCount = s.VerificationCheckCount,
        DependencyCount = s.DependencyCount,
        AffectedEntityCount = s.AffectedEntityCount,
        AffectedBoundaryCount = s.AffectedBoundaryCount
    };

    private static ApplicationDto ToDto(ApplicationMigrationSummary a) => new()
    {
        BoundaryId = a.Assessment.ApplicationBoundaryId,
        ApplicationName = a.Assessment.ApplicationBoundaryName,
        MigrationStatus = a.Assessment.OverallStatus.ToString(),
        RiskSeverity = a.RiskSeverity.ToString(),
        AffectedEntityCount = a.Assessment.AffectedEntityCount,
        AffectedBoundaryCount = a.Assessment.AffectedBoundaryCount,
        Issues = a.Assessment.Issues.Select(ToDto).ToList(),
        Dependencies = a.Assessment.Dependencies.Select(ToDto).ToList(),
        Actions = a.Actions.Select(ToDto).ToList(),
        PreMigrationChecks = a.PreMigrationChecks.Select(ToDto).ToList(),
        PostMigrationChecks = a.PostMigrationChecks.Select(ToDto).ToList()
    };

    private static IssueDto ToDto(MigrationIssue i) => new()
    {
        IssueId = i.IssueId,
        Title = i.Title,
        Description = i.Description,
        Severity = i.Severity.ToString(),
        MigrationStatusImpact = i.MigrationStatusImpact.ToString(),
        RuleId = i.RuleId,
        SourceRiskFindingId = i.SourceRiskFindingId,
        AffectedBoundaryIds = i.AffectedBoundaryIds.ToList(),
        AffectedEntityIds = i.AffectedEntityIds.ToList(),
        Evidence = i.Evidence.Select(ToDto).ToList(),
        Confidence = ToDto(i.Confidence),
        RequiredAction = i.RequiredAction,
        PolicyDecisionReason = i.PolicyDecisionReason
    };

    private static DependencyDto ToDto(MigrationDependency d) => new()
    {
        DependencyId = d.DependencyId,
        Type = d.Type.ToString(),
        Target = d.Target,
        AffectedBoundaryIds = d.AffectedBoundaryIds.ToList(),
        Confidence = ToDto(d.Confidence),
        Evidence = d.Evidence.Select(ToDto).ToList(),
        VerificationPhase = d.VerificationPhase.ToString(),
        VerificationRequirement = d.VerificationRequirement,
        RelatedRiskFindingId = d.RelatedRiskFindingId
    };

    private static DependencyGroupDto ToDto(MigrationDependencyGroup g) => new()
    {
        Type = g.Type.ToString(),
        Dependencies = g.Dependencies.Select(ToDto).ToList()
    };

    private static ActionDto ToDto(MigrationAction a) => new()
    {
        ActionId = a.ActionId,
        ActionType = a.ActionType.ToString(),
        Title = a.Title,
        Description = a.Description,
        Priority = a.Priority.ToString(),
        Phase = a.Phase.ToString(),
        AffectedBoundaryIds = a.AffectedBoundaryIds.ToList(),
        AffectedEntityIds = a.AffectedEntityIds.ToList(),
        RelatedIssueIds = a.RelatedIssueIds.ToList(),
        RelatedDependencyIds = a.RelatedDependencyIds.ToList(),
        Evidence = a.Evidence.Select(ToDto).ToList(),
        Rationale = a.Rationale
    };

    private static CheckDto ToDto(MigrationVerificationCheck c) => new()
    {
        CheckId = c.CheckId,
        Title = c.Title,
        Description = c.Description,
        Phase = c.Phase.ToString(),
        CheckType = c.CheckType.ToString(),
        AffectedBoundaryIds = c.AffectedBoundaryIds.ToList(),
        RelatedActionIds = c.RelatedActionIds.ToList(),
        RelatedDependencyIds = c.RelatedDependencyIds.ToList(),
        Evidence = c.Evidence.Select(ToDto).ToList(),
        Rationale = c.Rationale
    };

    private static CoverageWarningDto ToDto(CoverageWarning w) => new()
    {
        ScannerId = w.ScannerId,
        ScannerStatus = w.ScannerStatus.ToString(),
        Reason = w.Reason,
        AffectedPlatform = w.AffectedPlatform,
        Evidence = w.Evidence.ToList()
    };

    private static GraphValidationFindingDto ToDto(ValidationFinding f) => new()
    {
        Category = f.Category,
        Code = f.Code,
        Severity = f.Severity.ToString(),
        Message = f.Message,
        EntityIds = f.EntityIds.ToList()
    };

    private static DiagnosticsDto ToDto(ConsolidationDiagnostics d) => new()
    {
        ApplicationsConsolidated = d.ApplicationsConsolidated,
        ServerLevelIssueCount = d.ServerLevelIssueCount,
        SharedInfrastructureDependencyCount = d.SharedInfrastructureDependencyCount,
        CoverageWarningCount = d.CoverageWarningCount,
        GraphValidationErrorCount = d.GraphValidationErrorCount
    };

    private static EvidenceDto ToDto(EvidenceRecord e) => new()
    {
        Type = e.Type.ToString(),
        Location = e.Location,
        Detail = e.Detail,
        CapturedAt = e.CapturedAt
    };

    private static ConfidenceDto ToDto(Confidence c) => new()
    {
        Value = c.Value,
        Band = c.Band.ToString()
    };
}
