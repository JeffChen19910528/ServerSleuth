using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Tests.Fixtures;

namespace ServerSleuth.Analysis.Tests.Correlation;

public class CorrelationContextTests
{
    [Fact]
    public void TryResolveDllByPath_CaseInsensitiveMatch_Resolves()
    {
        var dll = EntityFactory.Dll(@"D:\ERP\Foo.dll");
        var context = new CorrelationContext([dll]);

        var resolved = context.TryResolveDllByPath(@"d:\erp\foo.dll");

        Assert.Same(dll, resolved);
    }

    [Fact]
    public void TryResolveDllByPath_NoMatch_ReturnsNull()
    {
        var context = new CorrelationContext([]);

        Assert.Null(context.TryResolveDllByPath(@"D:\ERP\Missing.dll"));
    }

    [Fact]
    public void NormalizeThumbprint_StripsWhitespaceAndUppercases()
    {
        Assert.Equal("AABBCC", CorrelationContext.NormalizeThumbprint("aa bb cc"));
    }
}
