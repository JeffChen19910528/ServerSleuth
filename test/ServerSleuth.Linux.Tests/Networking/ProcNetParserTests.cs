using ServerSleuth.Linux.Networking;

namespace ServerSleuth.Linux.Tests.Networking;

public class ProcNetParserTests
{
    private const string Header = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode";

    [Fact]
    public void Parse_Ipv4ListeningRow_DecodesAddressAndPort()
    {
        const string text = Header + "\n   0: 0100007F:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000     0        0 12345 1 0 0 10 0";

        var rows = ProcNetParser.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal("127.0.0.1", row.LocalAddress);
        Assert.Equal(8080, row.LocalPort);
        Assert.Equal("0A", row.StateHex);
        Assert.Equal("12345", row.Inode);
    }

    [Fact]
    public void Parse_Ipv6LoopbackRow_DecodesToCanonicalForm()
    {
        const string text = Header + "\n   0: 00000000000000000000000001000000:0050 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000     0        0 99999 1 0 0 10 0";

        var rows = ProcNetParser.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal("::1", row.LocalAddress);
        Assert.Equal(80, row.LocalPort);
    }

    [Fact]
    public void Parse_UdpRow_HasNonListenStateButIsStillParsed()
    {
        const string text = Header + "\n   0: 00000000:0035 00000000:0000 07 00000000:00000000 00:00000000 00000000     0        0 5555 1 0 0 10 0";

        var rows = ProcNetParser.Parse(text);

        var row = Assert.Single(rows);
        Assert.Equal(53, row.LocalPort);
        Assert.Equal("07", row.StateHex);
    }

    [Fact]
    public void Parse_MalformedRow_IsSkippedWithoutThrowing()
    {
        const string text = Header + "\n   0: not-a-valid-address 00000000:0000 0A 00000000:00000000 00:00000000 00000000     0        0 1 1 0 0 10 0";

        var rows = ProcNetParser.Parse(text);

        Assert.Empty(rows);
    }

    [Fact]
    public void Parse_OnlyHeader_ReturnsEmpty()
    {
        Assert.Empty(ProcNetParser.Parse(Header));
    }
}
