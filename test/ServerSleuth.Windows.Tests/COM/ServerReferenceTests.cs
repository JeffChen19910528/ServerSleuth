using ServerSleuth.Windows.COM;

namespace ServerSleuth.Windows.Tests.COM;

public class ServerReferenceTests
{
    [Fact]
    public void Parse_PlainPathNoSpaces_ReturnsPathWithNoArguments()
    {
        var result = ServerReference.Parse(@"C:\Windows\System32\combase.dll");

        Assert.Equal(@"C:\Windows\System32\combase.dll", result.ExecutablePath);
        Assert.Null(result.Arguments);
        Assert.False(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_QuotedPathWithSpaces_ExtractsPathWithoutArguments()
    {
        var result = ServerReference.Parse(@"""C:\Program Files\Vendor\Component.dll""");

        Assert.Equal(@"C:\Program Files\Vendor\Component.dll", result.ExecutablePath);
        Assert.Null(result.Arguments);
        Assert.False(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_QuotedPathWithArguments_SeparatesPathAndArguments()
    {
        var result = ServerReference.Parse(@"""C:\Program Files\Vendor\ComponentServer.exe"" /automation -mode:full");

        Assert.Equal(@"C:\Program Files\Vendor\ComponentServer.exe", result.ExecutablePath);
        Assert.Equal("/automation -mode:full", result.Arguments);
        Assert.False(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_UnquotedPathWithSpaces_IsAmbiguousAndNotGuessed()
    {
        var result = ServerReference.Parse(@"C:\Program Files\Vendor\Component.dll");

        Assert.Null(result.ExecutablePath);
        Assert.True(result.RawReferenceDetected);
        Assert.Equal(@"C:\Program Files\Vendor\Component.dll", result.RawValue);
    }

    [Fact]
    public void Parse_UnterminatedQuote_IsMalformedAndNotGuessed()
    {
        var result = ServerReference.Parse(@"""C:\Broken\Path.dll");

        Assert.Null(result.ExecutablePath);
        Assert.True(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_EmptyValue_IsAmbiguous()
    {
        var result = ServerReference.Parse("   ");

        Assert.Null(result.ExecutablePath);
        Assert.True(result.RawReferenceDetected);
    }

    [Fact]
    public void Parse_PreservesOriginalRawValueRegardlessOfOutcome()
    {
        const string raw = @"C:\Some Path\With Spaces\App.exe -arg";

        var result = ServerReference.Parse(raw);

        Assert.Equal(raw, result.RawValue);
    }
}
