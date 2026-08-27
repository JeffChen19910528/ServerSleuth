using ServerSleuth.Windows.Certificates;

namespace ServerSleuth.Windows.Tests.Certificates;

public class WindowsCertificateScannerBuildEntityTests
{
    private static readonly DateTimeOffset ScanDate = new(2026, 8, 23, 0, 0, 0, TimeSpan.Zero);

    private static CertificateRow MakeRow(
        string thumbprint = "AB12CD34EF56AB12CD34EF56AB12CD34EF56AB12",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null) => new()
    {
        Thumbprint = thumbprint,
        Subject = "CN=erp.company.com",
        Issuer = "CN=Contoso CA",
        SerialNumber = "01A2B3",
        NotBefore = notBefore ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        NotAfter = notAfter ?? new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        HasPrivateKey = true,
        SignatureAlgorithm = "sha256RSA",
        PublicKeyAlgorithm = "RSA",
        KeySizeBits = 2048,
        SubjectAlternativeNames = ["DNS Name=erp.company.com", "DNS Name=*.erp.company.com"],
        FriendlyName = "ERP SSL Certificate"
    };

    [Fact]
    public void BuildEntity_MapsCorePublicMetadataFields()
    {
        var entity = WindowsCertificateScanner.BuildEntity(MakeRow(), CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal("CN=erp.company.com", entity.Subject);
        Assert.Equal("CN=Contoso CA", entity.Issuer);
        Assert.Equal(2, entity.SubjectAlternativeNames.Count);
        Assert.Equal("ERP SSL Certificate", entity.Name);
    }

    [Fact]
    public void BuildEntity_NeverExposesPrivateKeyMaterial_OnlyTheFlag()
    {
        var entity = WindowsCertificateScanner.BuildEntity(MakeRow(), CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal("True", entity.Metadata["HasPrivateKey"]);
        Assert.DoesNotContain(entity.Metadata.Keys, k => k.Contains("PrivateKey") && k != "HasPrivateKey");
    }

    [Fact]
    public void BuildEntity_ThumbprintIsNormalizedToUppercaseNoWhitespace()
    {
        var entity = WindowsCertificateScanner.BuildEntity(MakeRow(thumbprint: " ab 12 cd 34 "), CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal("AB12CD34", entity.Thumbprint);
    }

    [Fact]
    public void BuildEntity_RecordsStoreAndStoreLocation()
    {
        var entity = WindowsCertificateScanner.BuildEntity(MakeRow(), CertificateStoreSource.CurrentUserMy, ScanDate);

        Assert.Equal("My", entity.Metadata["Store"]);
        Assert.Equal("CurrentUser", entity.Metadata["StoreLocation"]);
    }

    [Fact]
    public void BuildEntity_SameThumbprintDifferentStores_ProducesDistinctIds()
    {
        var row = MakeRow();

        var machineEntity = WindowsCertificateScanner.BuildEntity(row, CertificateStoreSource.LocalMachineMy, ScanDate);
        var userEntity = WindowsCertificateScanner.BuildEntity(row, CertificateStoreSource.CurrentUserMy, ScanDate);

        Assert.NotEqual(machineEntity.Id, userEntity.Id);
        Assert.Equal(machineEntity.Thumbprint, userEntity.Thumbprint); // same logical cert, matchable by Phase 5 later
    }

    [Theory]
    [InlineData(2027, 1, 1, "Valid")]
    [InlineData(2026, 1, 1, "Expired")]
    public void BuildEntity_RecordsCertificateStatus(int year, int month, int day, string expectedStatus)
    {
        var row = MakeRow(notAfter: new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero));

        var entity = WindowsCertificateScanner.BuildEntity(row, CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal(expectedStatus, entity.Metadata["CertificateStatus"]);
    }

    [Fact]
    public void BuildEntity_RecordsRiskLevelFromExpiryClassifier()
    {
        var row = MakeRow(notAfter: ScanDate.AddDays(5));

        var entity = WindowsCertificateScanner.BuildEntity(row, CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal("Critical", entity.Metadata["RiskLevel"]);
    }

    [Fact]
    public void BuildEntity_NoFriendlyName_FallsBackToSubjectForName()
    {
        var row = MakeRow() with { FriendlyName = null };

        var entity = WindowsCertificateScanner.BuildEntity(row, CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal("CN=erp.company.com", entity.Name);
        Assert.False(entity.Metadata.ContainsKey("FriendlyName"));
    }

    [Fact]
    public void BuildEntity_KeySizeAndAlgorithms_AreRecordedWhenAvailable()
    {
        var entity = WindowsCertificateScanner.BuildEntity(MakeRow(), CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.Equal("2048", entity.Metadata["KeySizeBits"]);
        Assert.Equal("sha256RSA", entity.Metadata["SignatureAlgorithm"]);
        Assert.Equal("RSA", entity.Metadata["PublicKeyAlgorithm"]);
    }

    [Fact]
    public void BuildEntity_MissingOptionalFields_DoesNotThrow()
    {
        var row = MakeRow() with { SerialNumber = null, SignatureAlgorithm = null, PublicKeyAlgorithm = null, KeySizeBits = null, FriendlyName = null };

        var entity = WindowsCertificateScanner.BuildEntity(row, CertificateStoreSource.LocalMachineMy, ScanDate);

        Assert.False(entity.Metadata.ContainsKey("SerialNumber"));
        Assert.False(entity.Metadata.ContainsKey("KeySizeBits"));
    }
}
