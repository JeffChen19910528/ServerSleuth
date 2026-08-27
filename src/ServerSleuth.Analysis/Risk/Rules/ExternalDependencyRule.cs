using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR9 (skill.md (Phase 7A) §20): classifies migration-sensitive external dependencies — NOT
/// an outage/health check (discovery never probes these endpoints, so their live availability
/// is unknown and unclaimed). Every already-extracted <see cref="ExternalDependency"/> of a
/// known kind produces exactly one Info-or-higher finding whose purpose is "this dependency
/// must be reconfigured/re-pointed for the target environment," not "this is currently down."
/// Host/Port/Database/Share details are surfaced only from the entity's own already-normalized
/// metadata — never a raw connection string, never a credential.
/// </summary>
public sealed class ExternalDependencyRule : IRiskRule
{
    public string Id => "RR9-ExternalDependency";
    public RiskCategory Category => RiskCategory.ExternalDependency;
    public RiskSeverity DefaultSeverity => RiskSeverity.Medium;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var dependency in context.ExternalDependencies)
        {
            var severity = dependency.Kind switch
            {
                ExternalDependencyKinds.FileShare => RiskSeverity.High,
                ExternalDependencyKinds.Ldap => RiskSeverity.High,
                ExternalDependencyKinds.Database => RiskSeverity.Medium,
                ExternalDependencyKinds.Redis => RiskSeverity.Medium,
                ExternalDependencyKinds.ExternalApi => RiskSeverity.Medium,
                _ => (RiskSeverity?)null
            };

            if (severity is null)
            {
                continue; // Unknown/unclassified kind — never guessed at
            }

            var isBoundaryMember = context.BoundaryIdByEntityId.TryGetValue(dependency.Id, out var boundaryId);

            var details = new List<string>();
            foreach (var key in new[] { "Host", "Port", "Database", "Server", "Share", "Path", "Scheme" })
            {
                if (dependency.Metadata.TryGetValue(key, out var value))
                {
                    details.Add($"{key}={value}");
                }
            }

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId(Id, dependency.Id),
                RuleId = Id,
                Category = Category,
                Severity = severity.Value,
                Confidence = dependency.Confidence,
                Title = $"External {dependency.Kind} dependency: {dependency.Name}",
                Description = $"This server depends on an external {dependency.Kind} endpoint ({dependency.Name}). This dependency must be reconfigured or re-pointed to an equivalent resource in the target environment during migration.",
                SourceEntityId = dependency.Id,
                ApplicationBoundaryId = boundaryId,
                Evidence = dependency.Evidence.Count > 0
                    ? dependency.Evidence
                    : [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = dependency.Id, Detail = string.Join(";", details) }],
                Recommendation = $"Confirm connectivity and credentials for this {dependency.Kind} endpoint are provisioned in the target environment before cutover.",
                Metadata = details.Count > 0 ? details.ToDictionary(d => d.Split('=')[0], d => d.Split('=', 2)[1]) : new Dictionary<string, string>()
            });
        }

        return findings;
    }
}
