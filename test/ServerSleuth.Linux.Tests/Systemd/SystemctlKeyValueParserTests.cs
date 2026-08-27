using ServerSleuth.Linux.Systemd;

namespace ServerSleuth.Linux.Tests.Systemd;

public class SystemctlKeyValueParserTests
{
    [Fact]
    public void Parse_TypicalShowOutput_ExtractsAllProperties()
    {
        const string text = """
                             Description=Nginx web server
                             LoadState=loaded
                             ActiveState=active
                             SubState=running
                             """;

        var result = SystemctlKeyValueParser.Parse(text);

        Assert.Equal("Nginx web server", result["Description"]);
        Assert.Equal("loaded", result["LoadState"]);
        Assert.Equal("active", result["ActiveState"]);
    }

    [Fact]
    public void Parse_ValueContainingEqualsSign_KeepsFullValue()
    {
        var result = SystemctlKeyValueParser.Parse("Environment=FOO=bar BAZ=qux");

        Assert.Equal("FOO=bar BAZ=qux", result["Environment"]);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmptyDictionary()
    {
        Assert.Empty(SystemctlKeyValueParser.Parse(string.Empty));
    }
}
