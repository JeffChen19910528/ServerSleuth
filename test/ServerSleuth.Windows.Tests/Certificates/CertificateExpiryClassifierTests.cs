using ServerSleuth.Windows.Certificates;

namespace ServerSleuth.Windows.Tests.Certificates;

public class CertificateExpiryClassifierTests
{
    private static readonly DateTimeOffset ScanDate = new(2026, 8, 23, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ClassifyStatus_WithinValidityWindow_ReturnsValid()
    {
        var status = CertificateExpiryClassifier.ClassifyStatus(
            notBefore: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            notAfter: new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            asOf: ScanDate);

        Assert.Equal("Valid", status);
    }

    [Fact]
    public void ClassifyStatus_BeforeNotBefore_ReturnsNotYetValid()
    {
        var status = CertificateExpiryClassifier.ClassifyStatus(
            notBefore: new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            notAfter: new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero),
            asOf: ScanDate);

        Assert.Equal("NotYetValid", status);
    }

    [Fact]
    public void ClassifyStatus_AfterNotAfter_ReturnsExpired()
    {
        var status = CertificateExpiryClassifier.ClassifyStatus(
            notBefore: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            notAfter: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            asOf: ScanDate);

        Assert.Equal("Expired", status);
    }

    [Theory]
    [InlineData(2026, 12, 1, "Normal")] // ~100 days out
    [InlineData(2026, 11, 1, "Warning")] // ~70 days out
    [InlineData(2026, 9, 10, "High")] // ~18 days out
    [InlineData(2026, 8, 28, "Critical")] // ~5 days out
    [InlineData(2026, 8, 1, "Critical")] // already expired
    public void ClassifyRiskLevel_MatchesSkillMdBands(int year, int month, int day, string expected)
    {
        var notAfter = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);

        var risk = CertificateExpiryClassifier.ClassifyRiskLevel(notAfter, ScanDate);

        Assert.Equal(expected, risk);
    }

    [Fact]
    public void ClassifyRiskLevel_ExactlyNinetyDaysOut_IsWarningNotNormal()
    {
        var notAfter = ScanDate.AddDays(90);

        Assert.Equal("Warning", CertificateExpiryClassifier.ClassifyRiskLevel(notAfter, ScanDate));
    }

    [Fact]
    public void ClassifyRiskLevel_ExactlySevenDaysOut_IsCritical()
    {
        var notAfter = ScanDate.AddDays(7);

        Assert.Equal("Critical", CertificateExpiryClassifier.ClassifyRiskLevel(notAfter, ScanDate));
    }
}
