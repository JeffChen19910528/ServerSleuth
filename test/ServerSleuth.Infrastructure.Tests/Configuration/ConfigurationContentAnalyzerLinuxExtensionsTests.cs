using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Infrastructure.Tests.Configuration;

/// <summary>Covers the Phase 6E extensions to `ConfigurationContentAnalyzer` (bare/braced/
/// default-value environment variable references, Unix sockets, NFS/CIFS network storage) —
/// the pre-existing Windows-oriented behavior (endpoints, UNC paths, secrets, connection
/// strings) is already covered by ServerSleuth.Windows.Tests's ConfigurationContentAnalyzerTests
/// and is unchanged here.</summary>
public class ConfigurationContentAnalyzerLinuxExtensionsTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    [Fact]
    public void Analyze_BareDollarVar_IsDetectedAsEnvironmentVariableReference()
    {
        var result = ConfigurationContentAnalyzer.Analyze("DB_HOST=$DB_HOST_OVERRIDE", Redactor);

        Assert.Contains("DB_HOST_OVERRIDE", result.EnvironmentVariableReferences);
    }

    [Fact]
    public void Analyze_BracedVar_IsDetectedAsEnvironmentVariableReference()
    {
        var result = ConfigurationContentAnalyzer.Analyze("path = ${HOME}/data", Redactor);

        Assert.Contains("HOME", result.EnvironmentVariableReferences);
    }

    [Fact]
    public void Analyze_BracedVarWithDefault_CapturesNameOnly_NeverTheDefaultText()
    {
        var result = ConfigurationContentAnalyzer.Analyze("port = ${APP_PORT:-8080}", Redactor);

        Assert.Contains("APP_PORT", result.EnvironmentVariableReferences);
        Assert.DoesNotContain(result.EnvironmentVariableReferences, v => v.Contains("8080"));
    }

    [Fact]
    public void Analyze_BareAndBracedFormsOfSameVariable_NeverDoubleCounted()
    {
        var result = ConfigurationContentAnalyzer.Analyze("a=$FOO b=${FOO}", Redactor);

        Assert.Single(result.EnvironmentVariableReferences, v => v == "FOO");
    }

    [Fact]
    public void Analyze_UnixSocketPath_IsDetected()
    {
        var result = ConfigurationContentAnalyzer.Analyze("listen unix:/var/run/mysqld/mysqld.sock;", Redactor);

        Assert.Contains(result.UnixSocketReferences, s => s == "/var/run/mysqld/mysqld.sock");
    }

    [Fact]
    public void Analyze_RunShapedSocket_IsDetected()
    {
        var result = ConfigurationContentAnalyzer.Analyze("SocketPath=/run/erp/app.sock", Redactor);

        Assert.Contains("/run/erp/app.sock", result.UnixSocketReferences);
    }

    [Fact]
    public void Analyze_NfsReference_IsDetectedAsNetworkStorage()
    {
        var result = ConfigurationContentAnalyzer.Analyze("mount nfs.internal:/exports/erp-data", Redactor);

        var reference = Assert.Single(result.NetworkStorageReferences);
        Assert.Equal("NFS", reference.Protocol);
        Assert.Equal("nfs.internal", reference.Server);
        Assert.Equal("/exports/erp-data", reference.Path);
    }

    [Fact]
    public void Analyze_CifsReference_IsDetectedAsNetworkStorage_DistinctFromNfs()
    {
        var result = ConfigurationContentAnalyzer.Analyze("share //fileserver/erp-share/data", Redactor);

        var reference = Assert.Single(result.NetworkStorageReferences);
        Assert.Equal("CIFS", reference.Protocol);
        Assert.Equal("fileserver", reference.Server);
    }

    [Fact]
    public void Analyze_HttpsUrl_NeverMisidentifiedAsNfsReference()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"""apiUrl"": ""https://erp-api.company.com/v1""", Redactor);

        Assert.DoesNotContain(result.NetworkStorageReferences, n => n.Server == "https");
        Assert.Single(result.ExternalEndpoints);
    }

    [Fact]
    public void Analyze_NeverConflatesUncAndCifsNotation()
    {
        var result = ConfigurationContentAnalyzer.Analyze(@"windowsShare = \\fileserver\share; linuxShare = //fileserver2/share", Redactor);

        Assert.Single(result.NetworkPaths); // UNC (Windows) form
        Assert.Single(result.NetworkStorageReferences, r => r.Protocol == "CIFS"); // CIFS (Linux) form
    }
}
