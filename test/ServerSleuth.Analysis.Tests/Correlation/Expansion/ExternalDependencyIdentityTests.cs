using ServerSleuth.Analysis.Correlation.Expansion;

namespace ServerSleuth.Analysis.Tests.Correlation.Expansion;

public class ExternalDependencyIdentityTests
{
    [Fact]
    public void ForDatabase_WithPortAndName_MatchesExpectedShape()
    {
        Assert.Equal("database:sqlserver:db01:1433:erp", ExternalDependencyIdentity.ForDatabase("database", "SqlServer", "DB01", 1433, "ERP"));
    }

    [Fact]
    public void ForDatabase_MissingPort_OmitsPortSegment_NeverGuesses()
    {
        var id = ExternalDependencyIdentity.ForDatabase("database", "PostgreSql", "DB02", null, "erp");

        Assert.DoesNotContain("::", id);
        Assert.Equal("database:postgresql:db02:erp", id);
    }

    [Fact]
    public void ForScopedHost_ApiWithPort_MatchesExpectedShape()
    {
        Assert.Equal("api:https:api.example.com:443", ExternalDependencyIdentity.ForScopedHost("api", "https", "api.example.com", 443));
    }

    [Fact]
    public void ForFileShare_MatchesExpectedShape()
    {
        Assert.Equal(@"fileshare:\\fileserver\erpdata", ExternalDependencyIdentity.ForFileShare("FILESERVER", "ERPData"));
    }

    [Fact]
    public void ForDatabase_CaseDifference_ProducesSameId()
    {
        var a = ExternalDependencyIdentity.ForDatabase("database", "SqlServer", "DB01", 1433, "ERP");
        var b = ExternalDependencyIdentity.ForDatabase("database", "sqlserver", "db01", 1433, "erp");

        Assert.Equal(a, b);
    }
}
