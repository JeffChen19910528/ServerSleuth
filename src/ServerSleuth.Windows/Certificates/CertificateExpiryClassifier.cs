namespace ServerSleuth.Windows.Certificates;

/// <summary>
/// Pure classification helper, deliberately separate from WindowsCertificateScanner so
/// expiry/risk rules aren't hard-coded into the scanner itself — see skill.md §16. This is
/// not the full Migration Risk Engine (Phase 7); it only labels a single certificate's own
/// validity/expiry state given an explicit "as of" time, so tests never depend on today's date.
/// </summary>
public static class CertificateExpiryClassifier
{
    public static string ClassifyStatus(DateTimeOffset notBefore, DateTimeOffset notAfter, DateTimeOffset asOf)
    {
        if (asOf < notBefore) return "NotYetValid";
        if (asOf > notAfter) return "Expired";
        return "Valid";
    }

    /// <summary>Migration-relevance risk banding per skill.md §16: >90d Normal, 31-90d
    /// Warning, 8-30d High, &lt;=7d or already expired Critical.</summary>
    public static string ClassifyRiskLevel(DateTimeOffset notAfter, DateTimeOffset asOf)
    {
        var daysRemaining = (notAfter - asOf).TotalDays;

        return daysRemaining switch
        {
            <= 7 => "Critical",
            <= 30 => "High",
            <= 90 => "Warning",
            _ => "Normal"
        };
    }
}
