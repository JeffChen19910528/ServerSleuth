using ServerSleuth.Linux.Packages;

namespace ServerSleuth.Linux.Tests.Packages;

public class ApkPackageLineParserTests
{
    [Theory]
    [InlineData("musl-1.2.4-r2", "musl", "1.2.4-r2")]
    [InlineData("openssl-3.1.4-r3", "openssl", "3.1.4-r3")]
    [InlineData("ca-certificates-20230506-r0", "ca-certificates", "20230506-r0")]
    [InlineData("libcrypto3-3.1.4-r3", "libcrypto3", "3.1.4-r3")]
    public void Parse_TypicalApkInfoLine_SplitsNameAndVersionCorrectly(string line, string expectedName, string expectedVersion)
    {
        var result = ApkPackageLineParser.Parse(line);

        Assert.NotNull(result);
        Assert.Equal(expectedName, result!.Value.Name);
        Assert.Equal(expectedVersion, result.Value.Version);
    }

    [Fact]
    public void Parse_UnrecognizedShape_ReturnsNull_NeverGuesses()
    {
        Assert.Null(ApkPackageLineParser.Parse("not-a-package-line-at-all"));
    }
}
