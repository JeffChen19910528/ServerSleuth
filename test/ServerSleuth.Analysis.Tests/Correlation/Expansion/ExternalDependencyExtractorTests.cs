using ServerSleuth.Analysis.Correlation.Expansion;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Correlation.Expansion;

public class ExternalDependencyExtractorTests
{
    private static Configuration NewConfiguration(string path = @"D:\ERP\Web\web.config") => new()
    {
        Id = $"configuration:{path}",
        Name = ServerSleuth.Analysis.Correlation.WindowsPathNormalizer.GetFileName(path),
        Type = "Configuration",
        Source = "FileSystem",
        Status = EntityStatus.Configured,
        Confidence = Confidence.High(),
        Path = path
    };

    [Fact]
    public void Extract_SqlServerDatabaseMetadata_ProducesDatabaseKindWithPortAndName()
    {
        var config = NewConfiguration();
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "ERP");

        var result = Assert.Single(ExternalDependencyExtractor.Extract(config));

        Assert.Equal(ExternalDependencyKinds.Database, result.Entity.Kind);
        Assert.Equal("database:sqlserver:db01:1433:erp", result.Entity.Id);
    }

    [Fact]
    public void Extract_RedisDatabaseMetadata_ProducesRedisKind_NeverStoresPassword()
    {
        var config = NewConfiguration();
        config.SetMetadata("Database0.Type", "Redis");
        config.SetMetadata("Database0.Host", "CACHE01");
        config.SetMetadata("Database0.Port", "6379");

        var result = Assert.Single(ExternalDependencyExtractor.Extract(config));

        Assert.Equal(ExternalDependencyKinds.Redis, result.Entity.Kind);
        Assert.Equal("redis:cache01:6379", result.Entity.Id);
        Assert.DoesNotContain(result.Entity.Metadata.Values, v => v.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_HttpsEndpoint_ProducesExternalApiKind()
    {
        var config = NewConfiguration();
        config.SetMetadata("Endpoint0.Scheme", "https");
        config.SetMetadata("Endpoint0.Host", "api.example.com");
        config.SetMetadata("Endpoint0.Port", "443");

        var result = Assert.Single(ExternalDependencyExtractor.Extract(config));

        Assert.Equal(ExternalDependencyKinds.ExternalApi, result.Entity.Kind);
        Assert.Equal("api:https:api.example.com:443", result.Entity.Id);
    }

    [Fact]
    public void Extract_LdapEndpoint_ProducesLdapKind()
    {
        var config = NewConfiguration();
        config.SetMetadata("Endpoint0.Scheme", "ldaps");
        config.SetMetadata("Endpoint0.Host", "dc01.corp.local");
        config.SetMetadata("Endpoint0.Port", "636");

        var result = Assert.Single(ExternalDependencyExtractor.Extract(config));

        Assert.Equal(ExternalDependencyKinds.Ldap, result.Entity.Kind);
    }

    [Fact]
    public void Extract_UncPath_ProducesFileShareKind()
    {
        var config = NewConfiguration();
        config.SetMetadata("NetworkPath0.Server", "FILESERVER");
        config.SetMetadata("NetworkPath0.Share", "ERPData");

        var result = Assert.Single(ExternalDependencyExtractor.Extract(config));

        Assert.Equal(ExternalDependencyKinds.FileShare, result.Entity.Kind);
        Assert.Equal(@"fileshare:\\fileserver\erpdata", result.Entity.Id);
    }

    [Fact]
    public void Extract_NoMetadata_ProducesNoDependencies()
    {
        var config = NewConfiguration();

        Assert.Empty(ExternalDependencyExtractor.Extract(config));
    }

    [Fact]
    public void Extract_EndpointWithNoHost_IsSkipped_NeverGuesses()
    {
        var config = NewConfiguration();
        config.SetMetadata("Endpoint0.Scheme", "https"); // host deliberately absent

        Assert.Empty(ExternalDependencyExtractor.Extract(config));
    }
}
