using ServerSleuth.Linux.OperatingSystem;

namespace ServerSleuth.Linux.Tests.OperatingSystem;

public class OsReleaseParserTests
{
    [Fact]
    public void Parse_TypicalUbuntuFile_ExtractsAllFields()
    {
        const string text = """
                             NAME="Ubuntu"
                             VERSION="22.04.3 LTS (Jammy Jellyfish)"
                             ID=ubuntu
                             VERSION_ID="22.04"
                             PRETTY_NAME="Ubuntu 22.04.3 LTS"
                             """;

        var result = OsReleaseParser.Parse(text);

        Assert.Equal("Ubuntu", result["NAME"]);
        Assert.Equal("ubuntu", result["ID"]);
        Assert.Equal("22.04", result["VERSION_ID"]);
        Assert.Equal("Ubuntu 22.04.3 LTS", result["PRETTY_NAME"]);
    }

    [Fact]
    public void Parse_UnquotedValues_AreAlsoAccepted()
    {
        var result = OsReleaseParser.Parse("ID=rhel\nVERSION_ID=9.3");

        Assert.Equal("rhel", result["ID"]);
        Assert.Equal("9.3", result["VERSION_ID"]);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmptyDictionary()
    {
        var result = OsReleaseParser.Parse(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MalformedLinesAndComments_AreSkippedWithoutThrowing()
    {
        const string text = """
                             # this is a comment
                             this line has no equals sign
                             =novalue
                             ID=ubuntu
                             """;

        var result = OsReleaseParser.Parse(text);

        Assert.Single(result);
        Assert.Equal("ubuntu", result["ID"]);
    }
}
