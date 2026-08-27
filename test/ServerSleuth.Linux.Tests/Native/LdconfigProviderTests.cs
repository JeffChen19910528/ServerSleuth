using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Native;

public class LdconfigProviderTests
{
    [Fact]
    public async Task GetCacheAsync_ParsesRealisticOutput()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("ldconfig", ["-p"], ProcessResult.Ok(0,
            "1000 libs found in cache `/etc/ld.so.cache'\n" +
            "\tlibz.so.1 (libc6,x86-64) => /lib/x86_64-linux-gnu/libz.so.1\n" +
            "\tlibssl.so.3 (libc6,x86-64) => /usr/lib/x86_64-linux-gnu/libssl.so.3\n",
            "", TimeSpan.Zero));

        var cache = await new LdconfigProvider(runner).GetCacheAsync(CancellationToken.None);

        Assert.Equal("/lib/x86_64-linux-gnu/libz.so.1", cache["libz.so.1"]);
        Assert.Equal("/usr/lib/x86_64-linux-gnu/libssl.so.3", cache["libssl.so.3"]);
    }

    [Fact]
    public async Task GetCacheAsync_LdconfigUnavailable_ReturnsEmptyCache_NeverThrows()
    {
        var runner = new FakeProcessRunner(); // nothing registered — default is StartFailedResult

        var cache = await new LdconfigProvider(runner).GetCacheAsync(CancellationToken.None);

        Assert.Empty(cache);
    }

    [Fact]
    public async Task GetCacheAsync_HeaderLine_NeverMisparsedAsALibraryEntry()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("ldconfig", ["-p"], ProcessResult.Ok(0, "1000 libs found in cache `/etc/ld.so.cache'\n", "", TimeSpan.Zero));

        var cache = await new LdconfigProvider(runner).GetCacheAsync(CancellationToken.None);

        Assert.Empty(cache);
    }

    [Fact]
    public async Task GetCacheAsync_NeverInvokesLdconfigWithoutDashP()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("ldconfig", ["-p"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));

        await new LdconfigProvider(runner).GetCacheAsync(CancellationToken.None);

        Assert.All(runner.Invocations, i => Assert.Equal(["-p"], i.Arguments));
    }
}
