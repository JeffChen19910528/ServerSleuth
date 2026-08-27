using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Tests.Configuration;

public class StructuralValidatorTests
{
    [Fact]
    public void TryValidateJson_WellFormedObject_ReturnsValidWithTopLevelSections()
    {
        const string json = """{ "connectionStrings": {}, "logging": {} }""";

        var (valid, sections) = StructuralValidator.TryValidateJson(json);

        Assert.True(valid);
        Assert.Contains("connectionStrings", sections);
        Assert.Contains("logging", sections);
    }

    [Fact]
    public void TryValidateJson_MalformedJson_ReturnsInvalidWithoutThrowing()
    {
        const string malformed = "{ \"key\": ";

        var (valid, sections) = StructuralValidator.TryValidateJson(malformed);

        Assert.False(valid);
        Assert.Empty(sections);
    }

    [Fact]
    public void TryValidateXml_WellFormedDocument_ReturnsValidWithTopLevelSections()
    {
        const string xml = "<configuration><connectionStrings/><appSettings/></configuration>";

        var (valid, sections) = StructuralValidator.TryValidateXml(xml);

        Assert.True(valid);
        Assert.Contains("connectionStrings", sections);
        Assert.Contains("appSettings", sections);
    }

    [Fact]
    public void TryValidateXml_MalformedXml_ReturnsInvalidWithoutThrowing()
    {
        const string malformed = "<configuration><unclosed>";

        var (valid, sections) = StructuralValidator.TryValidateXml(malformed);

        Assert.False(valid);
    }

    [Fact]
    public void TryValidateXml_DoctypeWithExternalEntity_IsRejectedNotResolved()
    {
        // A classic XXE payload: if DTD/external entity resolution were enabled, this could
        // read a local file or reach out over the network. DtdProcessing.Prohibit + a null
        // XmlResolver must cause this to fail parsing rather than resolve the entity.
        const string xxePayload = """
            <?xml version="1.0"?>
            <!DOCTYPE root [<!ENTITY xxe SYSTEM "file:///C:/Windows/win.ini">]>
            <root>&xxe;</root>
            """;

        var (valid, _) = StructuralValidator.TryValidateXml(xxePayload);

        Assert.False(valid); // DTD processing is prohibited, so this must not parse successfully
    }

    [Theory]
    [InlineData("appsettings.json", ConfigurationFormat.Json)]
    [InlineData("web.config", ConfigurationFormat.Xml)]
    [InlineData("settings.xml", ConfigurationFormat.Xml)]
    [InlineData("app.ini", ConfigurationFormat.Ini)]
    [InlineData("values.yaml", ConfigurationFormat.Yaml)]
    [InlineData("values.yml", ConfigurationFormat.Yaml)]
    [InlineData("app.properties", ConfigurationFormat.Properties)]
    [InlineData("notes.txt", ConfigurationFormat.Unknown)]
    public void FromFileName_MapsExtensionToFormat(string fileName, ConfigurationFormat expected)
    {
        Assert.Equal(expected, ConfigurationFormatDetector.FromFileName(fileName));
    }
}
