using ServerSleuth.Analysis.Risk.Diagnostics;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;

namespace ServerSleuth.Analysis.Risk.Engine;

/// <summary>
/// Executes every registered <see cref="IRiskRule"/> in deterministic Id order, enforces the
/// evidence invariant, deduplicates findings that share an explicit merge anchor, sorts the
/// result deterministically, and never lets one rule's failure abort the run — see skill.md
/// (Phase 7A) §10-11, §25-26.
/// </summary>
public sealed class RiskRuleEngine(IEnumerable<IRiskRule> rules)
{
    private readonly List<IRiskRule> _rules = rules.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();

    public RiskAnalysisResult Analyze(RiskAnalysisContext context)
    {
        var diagnostics = new RiskDiagnostics();
        var raw = new List<RiskFinding>();

        foreach (var rule in _rules)
        {
            diagnostics.RecordRuleEvaluated();

            IReadOnlyList<RiskFinding> findings;
            try
            {
                findings = rule.Evaluate(context);
            }
            catch (Exception ex)
            {
                diagnostics.RecordRuleFailure(rule.Id, $"{ex.GetType().Name}: {ex.Message}");
                continue; // one rule's failure never aborts the whole run
            }

            foreach (var finding in findings)
            {
                if (finding.Severity != RiskSeverity.Info && finding.Evidence.Count == 0)
                {
                    diagnostics.RecordEvidenceInvariantViolation(rule.Id, finding.Id, "Non-Info finding produced with zero evidence records — dropped rather than published without proof.");
                    continue;
                }

                raw.Add(finding);
                diagnostics.RecordFindingCreated();
            }
        }

        var deduplicated = Deduplicate(raw, diagnostics);

        var ordered = deduplicated
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Id, StringComparer.Ordinal)
            .ToList();

        return new RiskAnalysisResult { Findings = ordered, Diagnostics = diagnostics };
    }

    /// <summary>
    /// Two findings are only ever merged when they explicitly opt in by sharing the same
    /// <c>Metadata["MissingBinaryEntityId"]</c> anchor value — the one concrete cross-rule
    /// duplicate case skill.md (Phase 7A) §25 names (MissingBinaryRule and
    /// ServiceDependencyRule both identifying the same missing service executable). This is a
    /// deliberately narrow, explicit merge policy rather than a general fuzzy-matching system:
    /// findings that don't opt in are never merged, even if they look superficially similar,
    /// so provenance is never silently lost by an over-eager heuristic.
    /// </summary>
    private static List<RiskFinding> Deduplicate(List<RiskFinding> findings, RiskDiagnostics diagnostics)
    {
        var groups = findings.GroupBy(f => f.Metadata.TryGetValue("MissingBinaryEntityId", out var anchor) ? $"anchor:{anchor}" : $"solo:{f.Id}");

        var result = new List<RiskFinding>();
        var mergedAwayCount = 0;

        foreach (var group in groups)
        {
            var members = group.ToList();
            if (members.Count == 1)
            {
                result.Add(members[0]);
                continue;
            }

            mergedAwayCount += members.Count - 1;
            result.Add(Merge(members));
        }

        if (mergedAwayCount > 0)
        {
            diagnostics.RecordFindingsDeduplicated(mergedAwayCount);
        }

        return result;
    }

    private static RiskFinding Merge(List<RiskFinding> members)
    {
        var primary = members.OrderByDescending(f => f.Severity).ThenBy(f => f.RuleId, StringComparer.Ordinal).First();

        // Two distinct shapes reach this method via Deduplicate's grouping: an explicit
        // cross-rule merge opt-in (Metadata["MissingBinaryEntityId"], shared by
        // MissingBinaryRule/ServiceDependencyRule/ScheduledTaskDependencyRule/ComDependencyRule
        // — skill.md Phase 7A §25), or a same-Id collision within a single rule's own output
        // (a "solo:{f.Id}" group with more than one member — e.g. MissingDependencyRule emitting
        // one finding per unresolved import name for a real DLL that has several: every emission
        // shares the same SourceEntityId and therefore the same deterministic Id, since Id only
        // encodes RuleId+SourceEntityId, never which import triggered it). Only the first shape
        // carries an explicit anchor value in Metadata; the second has none. Both cases converge
        // on the finding's own SourceEntityId as the correct merge anchor — for a same-Id
        // collision, every member already shares one SourceEntityId by construction (that's what
        // made their Ids identical), so this is never a guess, just the value already agreed on.
        var anchor = primary.Metadata.TryGetValue("MissingBinaryEntityId", out var explicitAnchor)
            ? explicitAnchor
            : primary.SourceEntityId;

        var relatedIds = members
            .SelectMany(f => new[] { f.SourceEntityId }.Concat(f.RelatedEntityIds))
            .Where(id => !string.Equals(id, anchor, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var evidence = members
            .SelectMany(f => f.Evidence)
            .GroupBy(e => (e.Type, e.Location, e.Detail))
            .Select(g => g.First())
            .ToList();

        var contributingRules = members.Select(f => f.RuleId).Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList();
        var metadata = new Dictionary<string, string>(primary.Metadata) { ["ContributingRules"] = string.Join(",", contributingRules) };

        return primary with
        {
            Id = RiskFinding.ComputeId("merged", anchor, relatedIds),
            RuleId = primary.RuleId,
            Severity = members.Max(f => f.Severity),
            Confidence = members.Select(f => f.Confidence).OrderByDescending(c => c.Value).First(),
            SourceEntityId = anchor,
            RelatedEntityIds = relatedIds,
            Evidence = evidence,
            Metadata = metadata
        };
    }
}
