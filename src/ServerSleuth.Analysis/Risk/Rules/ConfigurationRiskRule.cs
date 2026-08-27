using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR11 (skill.md (Phase 7A) §22): surfaces migration-sensitive references already extracted
/// by Configuration discovery (Phase 4E-1/6E) — never re-parses a configuration file, never
/// re-scans anything. Complements <see cref="ExternalDependencyRule"/> (which flags the
/// extracted <c>ExternalDependency</c> entity itself) by flagging the owning Configuration file
/// from its own perspective; the two are deliberately not merged, since they answer different
/// questions ("which config files need attention" vs. "which external endpoints need
/// attention"). Network-storage/file-share references are High (an explicit path dependency);
/// environment-variable and Unix-socket references are Info/Low (common and, on their own,
/// rarely migration-blocking); already-classified endpoint/database references are Info here,
/// since <see cref="ExternalDependencyRule"/> already gives them their real severity.
/// </summary>
public sealed class ConfigurationRiskRule : IRiskRule
{
    public string Id => "RR11-ConfigurationRisk";
    public RiskCategory Category => RiskCategory.Configuration;
    public RiskSeverity DefaultSeverity => RiskSeverity.Low;

    private static readonly (string Prefix, RiskSeverity Severity, string Description)[] Markers =
    [
        ("FileShare: ", RiskSeverity.High, "references a Windows network file share"),
        ("NetworkStorage: ", RiskSeverity.High, "references a Linux network storage location (NFS/CIFS)"),
        ("UnixSocket: ", RiskSeverity.Low, "references a Unix domain socket path"),
        ("EnvVar: ", RiskSeverity.Info, "references an environment variable"),
        ("Endpoint: ", RiskSeverity.Info, "references an external endpoint (see the ExternalDependency finding for details)"),
        ("Database: ", RiskSeverity.Info, "references a database (see the ExternalDependency finding for details)")
    ];

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();

        foreach (var configuration in context.Configurations)
        {
            foreach (var reference in configuration.DetectedDependencyReferences)
            {
                var marker = Markers.FirstOrDefault(m => reference.StartsWith(m.Prefix, StringComparison.Ordinal));
                if (marker.Prefix is null)
                {
                    continue;
                }

                var value = reference[marker.Prefix.Length..];

                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, configuration.Id, [reference]),
                    RuleId = Id,
                    Category = Category,
                    Severity = marker.Severity,
                    Confidence = configuration.Confidence,
                    Title = $"Configuration {marker.Description}: {configuration.Name}",
                    Description = $"'{configuration.Name}' {marker.Description} ({value}). Review this reference for migration-readiness in the target environment.",
                    SourceEntityId = configuration.Id,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = configuration.Path ?? configuration.Id, Detail = reference }],
                    Recommendation = "Confirm this reference resolves correctly in the target environment, or update it as part of migration."
                });
            }
        }

        return findings;
    }
}
