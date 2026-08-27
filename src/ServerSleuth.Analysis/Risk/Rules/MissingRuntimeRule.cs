using System.Text.RegularExpressions;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR4 (skill.md (Phase 7A) §15): a Configuration explicitly names a target framework moniker
/// (e.g. "net8.0", via Phase 5C's <c>RuntimeVersionReferences</c> — stored as a
/// <c>"RuntimeVersion: net8.0"</c> entry in <c>DetectedDependencyReferences</c>) for which no
/// discovered Runtime's major version matches. Deliberately keyed ONLY off this explicit,
/// version-bearing reference — never off a bare family marker (`"Runtime: DotNet"`, produced by
/// a different, lower-confidence detection path), a directory name, a package name, or an
/// application name. An installed-but-unreferenced runtime is never itself a finding (see the
/// Expected Orphans policy, skill.md §24).
/// </summary>
public sealed class MissingRuntimeRule : IRiskRule
{
    private const string Prefix = "RuntimeVersion: ";
    private static readonly Regex NetVersionPattern = new(@"^net(?<major>\d+)(?:\.\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Id => "RR4-MissingRuntime";
    public RiskCategory Category => RiskCategory.MissingRuntime;
    public RiskSeverity DefaultSeverity => RiskSeverity.High;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();
        var installedMajors = context.Runtimes
            .Select(r => ParseMajor(r.Version))
            .Concat(context.Sdks.Select(s => ParseMajor(s.Version)))
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToHashSet();

        foreach (var configuration in context.Configurations)
        {
            foreach (var reference in configuration.DetectedDependencyReferences)
            {
                if (!reference.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var tfm = reference[Prefix.Length..];
                var match = NetVersionPattern.Match(tfm);
                if (!match.Success)
                {
                    continue; // not a recognized net-major-version shape — never guessed at
                }

                var major = int.Parse(match.Groups["major"].Value);
                if (installedMajors.Contains(major))
                {
                    continue;
                }

                findings.Add(new RiskFinding
                {
                    Id = RiskFinding.ComputeId(Id, configuration.Id, [$"tfm:{tfm}"]),
                    RuleId = Id,
                    Category = Category,
                    Severity = DefaultSeverity,
                    Confidence = Confidence.High(),
                    Title = $"Required runtime not present: {tfm}",
                    Description = $"'{configuration.Name}' explicitly targets '{tfm}', but no discovered .NET Runtime or SDK with major version {major} was found on this server.",
                    SourceEntityId = configuration.Id,
                    Evidence = [new EvidenceRecord { Type = EvidenceType.ConfigurationFile, Location = configuration.Path ?? configuration.Id, Detail = reference }],
                    Recommendation = $"Install .NET {major} runtime/SDK on the target environment, or confirm the target framework was upgraded/downgraded intentionally."
                });
            }
        }

        return findings;
    }

    private static int? ParseMajor(string? version)
    {
        if (version is null)
        {
            return null;
        }

        var dot = version.IndexOf('.');
        var majorText = dot >= 0 ? version[..dot] : version;
        return int.TryParse(majorText, out var major) ? major : null;
    }
}
