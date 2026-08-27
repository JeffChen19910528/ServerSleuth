using ServerSleuth.Analysis.Correlation;

namespace ServerSleuth.Analysis.Tests.Correlation;

public class WindowsPathNormalizerTests
{
    private sealed class FakeResolver(Dictionary<string, string> values) : IEnvironmentVariableResolver
    {
        public string? GetValue(string name) => values.GetValueOrDefault(name);
    }

    [Fact]
    public void Normalize_CaseDifference_ProducesSameComparisonKey()
    {
        var a = WindowsPathNormalizer.Normalize(@"C:\Vendor\Foo.dll");
        var b = WindowsPathNormalizer.Normalize(@"c:\vendor\foo.dll");

        Assert.Equal(a.ComparisonKey, b.ComparisonKey);
    }

    [Fact]
    public void Normalize_TrailingSeparator_ProducesSameComparisonKey()
    {
        var a = WindowsPathNormalizer.Normalize(@"C:\Vendor\Foo.dll");
        var b = WindowsPathNormalizer.Normalize(@"C:\Vendor\Foo.dll\");

        Assert.Equal(a.ComparisonKey, b.ComparisonKey);
    }

    [Fact]
    public void Normalize_QuotedPath_StripsQuotes()
    {
        var result = WindowsPathNormalizer.Normalize("\"C:\\Vendor\\Foo.dll\"");

        Assert.Equal(@"C:\Vendor\Foo.dll", result.Value);
    }

    [Fact]
    public void Normalize_ProgramFilesVsX86_NeverMerge()
    {
        var a = WindowsPathNormalizer.Normalize(@"C:\Program Files\Vendor");
        var b = WindowsPathNormalizer.Normalize(@"C:\Program Files (x86)\Vendor");

        Assert.NotEqual(a.ComparisonKey, b.ComparisonKey);
    }

    [Fact]
    public void Normalize_UncPaths_PreserveDistinctHosts()
    {
        var a = WindowsPathNormalizer.Normalize(@"\\ServerA\Share");
        var b = WindowsPathNormalizer.Normalize(@"\\ServerB\Share");

        Assert.NotEqual(a.ComparisonKey, b.ComparisonKey);
        Assert.True(a.IsUnc);
        Assert.True(b.IsUnc);
    }

    [Fact]
    public void Normalize_ResolvableEnvironmentVariable_ExpandsInValue()
    {
        var resolver = new FakeResolver(new Dictionary<string, string> { ["PROGRAMFILES"] = @"C:\Program Files" });

        var result = WindowsPathNormalizer.Normalize(@"%PROGRAMFILES%\Vendor\Foo.dll", resolver);

        Assert.Equal(@"C:\Program Files\Vendor\Foo.dll", result.Value);
        Assert.False(result.EnvironmentVariableUnresolved);
    }

    [Fact]
    public void Normalize_UnresolvableEnvironmentVariable_PreservesUnresolvedReference()
    {
        var resolver = new FakeResolver([]);

        var result = WindowsPathNormalizer.Normalize(@"%CUSTOM_UNKNOWN_VAR%\Vendor\Foo.dll", resolver);

        Assert.Contains("%CUSTOM_UNKNOWN_VAR%", result.Value);
        Assert.True(result.EnvironmentVariableUnresolved);
    }

    [Fact]
    public void Normalize_NullOrEmpty_ReturnsEmptyValue()
    {
        var result = WindowsPathNormalizer.Normalize(null);

        Assert.Equal(string.Empty, result.Value);
        Assert.Equal(string.Empty, result.ComparisonKey);
    }

    [Fact]
    public void Normalize_ForwardSlashes_NormalizedToBackslashes()
    {
        var result = WindowsPathNormalizer.Normalize("C:/Vendor/Foo.dll");

        Assert.Equal(@"C:\Vendor\Foo.dll", result.Value);
    }
}
