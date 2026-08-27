using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Risk.Rules;

/// <summary>
/// RR5 (skill.md (Phase 7A) §16): certificate expiry classification. No existing helper
/// classifies certificate expiry elsewhere in the codebase, so this rule owns the thresholds
/// directly (documented here, not scattered): already expired → Critical; expiring within 30
/// days → High; within 90 days → Medium; beyond that → no finding. A certificate with no
/// <c>ValidTo</c> at all is never guessed at (no finding). A not-yet-valid certificate
/// (<c>ValidFrom</c> in the future) is never flagged by this rule — skill.md §16 explicitly
/// reserves that judgment for a future signal the current Certificate model doesn't carry.
/// Confidence is VeryHigh: expiry dates are unambiguous scanner-read facts.
/// </summary>
public sealed class CertificateExpiryRule : IRiskRule
{
    private static readonly TimeSpan HighWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan MediumWindow = TimeSpan.FromDays(90);

    public string Id => "RR5-CertificateExpiry";
    public RiskCategory Category => RiskCategory.Certificate;
    public RiskSeverity DefaultSeverity => RiskSeverity.Medium;

    public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context)
    {
        var findings = new List<RiskFinding>();
        var now = DateTimeOffset.UtcNow;

        foreach (var certificate in context.Certificates)
        {
            if (certificate.ValidTo is not { } validTo)
            {
                continue; // unknown expiry — never guessed at
            }

            var remaining = validTo - now;
            RiskSeverity? severity = remaining switch
            {
                _ when remaining <= TimeSpan.Zero => RiskSeverity.Critical,
                _ when remaining <= HighWindow => RiskSeverity.High,
                _ when remaining <= MediumWindow => RiskSeverity.Medium,
                _ => null
            };

            if (severity is null)
            {
                continue;
            }

            var isBoundaryMember = context.BoundaryIdByEntityId.TryGetValue(certificate.Id, out var boundaryId);
            var state = remaining <= TimeSpan.Zero ? "has expired" : $"expires in {remaining.Days} day(s)";

            findings.Add(new RiskFinding
            {
                Id = RiskFinding.ComputeId(Id, certificate.Id),
                RuleId = Id,
                Category = Category,
                Severity = severity.Value,
                Confidence = Confidence.VeryHigh(),
                Title = $"Certificate {state}: {certificate.Subject ?? certificate.Name}",
                Description = $"Certificate '{certificate.Subject ?? certificate.Name}' (thumbprint {certificate.Thumbprint}) {state} on {validTo:yyyy-MM-dd}.",
                SourceEntityId = certificate.Id,
                ApplicationBoundaryId = boundaryId,
                Evidence = certificate.Evidence.Count > 0 ? certificate.Evidence : [new EvidenceRecord { Type = EvidenceType.CertificateStore, Location = certificate.Id, Detail = $"ValidTo={validTo:O}" }],
                Recommendation = "Renew this certificate before migration, or confirm the target environment will use a replacement certificate."
            });
        }

        return findings;
    }
}
