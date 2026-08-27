using ServerSleuth.Analysis.Correlation;

namespace ServerSleuth.Analysis.Tests.Correlation;

public class CommandLineReferenceTests
{
    [Fact]
    public void Parse_QuotedPathWithArguments_SplitsPathAndArguments()
    {
        var result = CommandLineReference.Parse("\"C:\\Program Files\\Vendor\\App.exe\" -k netsvcs");

        Assert.Equal(@"C:\Program Files\Vendor\App.exe", result.ExecutablePath);
        Assert.Equal("-k netsvcs", result.Arguments);
        Assert.False(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_UnquotedNoSpace_IsUnambiguous()
    {
        var result = CommandLineReference.Parse(@"C:\Windows\System32\svchost.exe");

        Assert.Equal(@"C:\Windows\System32\svchost.exe", result.ExecutablePath);
        Assert.False(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_UnquotedWithSpace_IsAmbiguous_DoesNotGuess()
    {
        var result = CommandLineReference.Parse(@"C:\Program Files\Vendor\App.exe -k netsvcs");

        Assert.Null(result.ExecutablePath);
        Assert.True(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_Empty_IsMarkedRaw()
    {
        var result = CommandLineReference.Parse("");

        Assert.Null(result.ExecutablePath);
        Assert.True(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_UnterminatedQuote_DoesNotGuess()
    {
        var result = CommandLineReference.Parse("\"C:\\Vendor\\App.exe");

        Assert.Null(result.ExecutablePath);
        Assert.True(result.RawReferenceDetected);
    }
}
