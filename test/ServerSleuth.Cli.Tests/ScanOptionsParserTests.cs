using ServerSleuth.Cli.Options;

namespace ServerSleuth.Cli.Tests;

/// <summary>Direct unit coverage of <see cref="ScanOptionsParser"/> — see skill.md (Phase 10A)
/// §6, §22.</summary>
public class ScanOptionsParserTests
{
    [Fact]
    public void Defaults_MatchDocumentedDefaults()
    {
        var options = ScanOptionsParser.Parse([]);

        Assert.Equal("./serversleuth-report", options.OutputDirectory);
        Assert.Equal(ReportFormatOption.Both, options.Format);
        Assert.False(options.Overwrite);
        Assert.False(options.Quiet);
    }

    [Theory]
    [InlineData("json", ReportFormatOption.Json)]
    [InlineData("JSON", ReportFormatOption.Json)]
    [InlineData("html", ReportFormatOption.Html)]
    [InlineData("both", ReportFormatOption.Both)]
    public void Format_ParsesCaseInsensitively(string value, ReportFormatOption expected)
    {
        var options = ScanOptionsParser.Parse(["--format", value]);
        Assert.Equal(expected, options.Format);
    }

    [Fact]
    public void InvalidFormat_ThrowsCliArgumentException()
    {
        var ex = Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--format", "xml"]));
        Assert.Contains("xml", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_SetsOutputDirectory()
    {
        var options = ScanOptionsParser.Parse(["--output", "/tmp/my-report"]);
        Assert.Equal("/tmp/my-report", options.OutputDirectory);
    }

    [Fact]
    public void Output_MissingValue_ThrowsCliArgumentException()
    {
        Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--output"]));
    }

    [Fact]
    public void Overwrite_SetsFlag_MapsToOverwritePolicy()
    {
        var options = ScanOptionsParser.Parse(["--overwrite"]);
        Assert.True(options.Overwrite);
        Assert.Equal(Reporting.Export.ReportOverwritePolicy.Overwrite, options.OverwritePolicy);
    }

    [Fact]
    public void Quiet_SetsFlag()
    {
        var options = ScanOptionsParser.Parse(["--quiet"]);
        Assert.True(options.Quiet);
    }

    [Fact]
    public void Verbose_SetsFlag()
    {
        var options = ScanOptionsParser.Parse(["--verbose"]);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void Verbose_DefaultsToFalse()
    {
        var options = ScanOptionsParser.Parse([]);
        Assert.False(options.Verbose);
    }

    [Fact]
    public void UnknownOption_ThrowsCliArgumentException()
    {
        var ex = Assert.Throws<CliArgumentException>(() => ScanOptionsParser.Parse(["--bogus"]));
        Assert.Contains("--bogus", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllOptions_CanBeCombined()
    {
        var options = ScanOptionsParser.Parse(["--output", "out", "--format", "html", "--overwrite", "--quiet", "--verbose"]);

        Assert.Equal("out", options.OutputDirectory);
        Assert.Equal(ReportFormatOption.Html, options.Format);
        Assert.True(options.Overwrite);
        Assert.True(options.Quiet);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void ParsingIsDeterministic_ForTheSameArguments()
    {
        var a = ScanOptionsParser.Parse(["--output", "out", "--format", "json"]);
        var b = ScanOptionsParser.Parse(["--output", "out", "--format", "json"]);

        Assert.Equal(a, b);
    }
}
