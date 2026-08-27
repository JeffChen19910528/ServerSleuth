using ServerSleuth.Linux.Process;

namespace ServerSleuth.Linux.Tests.Process;

public class ProcStatusParserTests
{
    [Fact]
    public void Parse_TypicalStatusFile_ExtractsFields()
    {
        const string text = "Name:\tnginx\nState:\tS (sleeping)\nPPid:\t1\nUid:\t0\t0\t0\t0\n";

        var fields = ProcStatusParser.Parse(text);

        Assert.Equal("nginx", fields["Name"]);
        Assert.Equal("S (sleeping)", fields["State"]);
        Assert.Equal("1", fields["PPid"]);
    }

    [Fact]
    public void ExtractRealUid_FourColumnUidLine_ReturnsFirstValue()
    {
        Assert.Equal("1000", ProcStatusParser.ExtractRealUid("1000\t1000\t1000\t1000"));
    }

    [Fact]
    public void ExtractRealUid_Null_ReturnsNull()
    {
        Assert.Null(ProcStatusParser.ExtractRealUid(null));
    }

    [Fact]
    public void Parse_MalformedLines_AreSkippedWithoutThrowing()
    {
        var fields = ProcStatusParser.Parse("garbage line with no colon\nName:\tsshd\n");

        Assert.Single(fields);
        Assert.Equal("sshd", fields["Name"]);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmptyDictionary()
    {
        Assert.Empty(ProcStatusParser.Parse(string.Empty));
    }
}
