using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Tests.Configuration;

public class ConfigurationContentAnalyzerTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    [Fact]
    public void Analyze_HttpsUrl_IsDetectedAsExternalEndpoint()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"""apiUrl"": ""https://erp-api.company.com:8443/v1""", Redactor);

        var endpoint = Assert.Single(result.ExternalEndpoints);
        Assert.Equal("https", endpoint.Scheme);
        Assert.Equal("erp-api.company.com", endpoint.Host);
        Assert.Equal(8443, endpoint.Port);
        Assert.Equal("/v1", endpoint.Path);
    }

    [Fact]
    public void Analyze_UncPath_IsParsedIntoServerAndShare()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"dataPath: \\FILESERVER01\ERPData\Inbox", Redactor);

        var unc = Assert.Single(result.NetworkPaths);
        Assert.Equal("FILESERVER01", unc.Server);
        Assert.Equal("ERPData", unc.Share);
    }

    [Theory]
    [InlineData("%PROGRAMDATA%\\Config", "PROGRAMDATA")]
    [InlineData("${JAVA_HOME}/bin", "JAVA_HOME")]
    public void Analyze_EnvironmentVariableReferences_AreDetected(string text, string expectedName)
    {
        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        Assert.Contains(expectedName, result.EnvironmentVariableReferences);
    }

    [Theory]
    [InlineData("JAVA_HOME=C:\\Java\\jdk-17", "Java")]
    [InlineData("DOTNET_ROOT=C:\\Program Files\\dotnet", "DotNet")]
    [InlineData("node_modules folder present", "Node")]
    public void Analyze_RuntimeMarkers_AreRecognized(string text, string expectedFamily)
    {
        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        Assert.Contains(expectedFamily, result.RuntimeReferences);
    }

    [Fact]
    public void Analyze_SqlServerConnectionString_IsClassifiedCorrectlyWithoutCredential()
    {
        const string text = "\"connectionString\": \"Server=db01;Initial Catalog=ErpDb;User Id=sa;Password=hunter2;\"";

        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        var db = Assert.Single(result.DatabaseReferences);
        Assert.Equal("SqlServer", db.Type);
        Assert.Equal("db01", db.Host);
        Assert.Equal("ErpDb", db.Database);
    }

    [Fact]
    public void Analyze_ConnectionStringNeverExposesPassword()
    {
        const string text = "\"connectionString\": \"Server=db01;Initial Catalog=ErpDb;User Id=sa;Password=hunter2;\"";

        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        var db = Assert.Single(result.DatabaseReferences);
        // DatabaseReference has no Password field at all by design — Host/Database are the
        // only string fields populated, and neither should ever carry the credential value.
        Assert.NotEqual("hunter2", db.Host);
        Assert.NotEqual("hunter2", db.Database);
    }

    [Fact]
    public void Analyze_PostgresConnectionString_IsClassified()
    {
        const string text = "\"connectionString\": \"Host=pg01;Port=5432;Database=erp;Username=app\"";

        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        Assert.Equal("PostgreSql", Assert.Single(result.DatabaseReferences).Type);
    }

    [Fact]
    public void Analyze_PasswordKey_SetsSecretDetected()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"""password"": ""hunter2""", Redactor);

        Assert.True(result.SecretDetected);
    }

    [Fact]
    public void Analyze_NoSecretShapedContent_SecretDetectedIsFalse()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"""maxConnections"": 100, ""timeout"": 30", Redactor);

        Assert.False(result.SecretDetected);
    }

    [Fact]
    public void Analyze_PlainConfiguration_ReturnsNoFalsePositives()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"""logLevel"": ""Information""", Redactor);

        Assert.Empty(result.ExternalEndpoints);
        Assert.Empty(result.NetworkPaths);
        Assert.Empty(result.DatabaseReferences);
        Assert.Empty(result.RuntimeReferences);
    }

    [Fact]
    public void Analyze_DuplicateEndpointsInText_AreDeduplicated()
    {
        const string text = "https://api.company.com https://api.company.com";

        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        Assert.Single(result.ExternalEndpoints);
    }

    [Theory]
    [InlineData("<TargetFramework>net8.0</TargetFramework>")]
    [InlineData("\"TargetFramework\": \"net8.0\"")]
    [InlineData("TargetFramework=net8.0")]
    public void Analyze_ExplicitTargetFrameworkMoniker_IsDetectedAsRuntimeVersionReference(string text)
    {
        var result = ConfigurationContentAnalyzer.Analyze(text, Redactor);

        Assert.Contains("net8.0", result.RuntimeVersionReferences);
    }

    [Fact]
    public void Analyze_BareNetVersionLookingTextWithNoTargetFrameworkKey_IsNotDetectedAsVersionReference()
    {
        // "net8.0"-shaped text with no TargetFramework key nearby must never be picked up —
        // otherwise incidental text (a comment, a URL segment) could fabricate a version match.
        var result = ConfigurationContentAnalyzer.Analyze(@"""notes"": ""upgrade planned for net8.0 next quarter""", Redactor);

        Assert.Empty(result.RuntimeVersionReferences);
    }

    [Fact]
    public void Analyze_FamilyMarkerWithNoExplicitVersion_ProducesFamilyReferenceButNoVersionReference()
    {
        var result = ConfigurationContentAnalyzer.Analyze("DOTNET_ROOT=C:\\Program Files\\dotnet", Redactor);

        Assert.Contains("DotNet", result.RuntimeReferences);
        Assert.Empty(result.RuntimeVersionReferences);
    }
}
