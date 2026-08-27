using ServerSleuth.Linux.Systemd;

namespace ServerSleuth.Linux.Tests.Systemd;

public class ExecStartParserTests
{
    [Fact]
    public void ExtractExecutablePath_TypicalExecStartShape_ReturnsPath()
    {
        const string execStart = "{ path=/usr/sbin/nginx ; argv[]=/usr/sbin/nginx -g daemon off; ; ignore_errors=no ; start_time=[n/a] }";

        var path = ExecStartParser.ExtractExecutablePath(execStart);

        Assert.Equal("/usr/sbin/nginx", path);
    }

    [Fact]
    public void ExtractExecutablePath_Null_ReturnsNull()
    {
        Assert.Null(ExecStartParser.ExtractExecutablePath(null));
    }

    [Fact]
    public void ExtractExecutablePath_UnrecognizedShape_ReturnsNull_NeverGuesses()
    {
        Assert.Null(ExecStartParser.ExtractExecutablePath("some unexpected free-form text"));
    }
}
