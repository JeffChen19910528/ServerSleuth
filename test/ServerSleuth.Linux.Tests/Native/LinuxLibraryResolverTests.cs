using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Native;

public class LinuxLibraryResolverTests
{
    private static readonly IReadOnlyDictionary<string, string> EmptyLdconfig = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyKnownBinaries = new Dictionary<string, IReadOnlyList<string>>();

    [Fact]
    public void Resolve_FoundInFirstRpathEntry_ReturnsResolvedWithRpathSource()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfo("/opt/erp/lib/libfoo.so");

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", null, ["/opt/erp/lib"], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.Resolved, result.Status);
        Assert.Equal("/opt/erp/lib/libfoo.so", result.ResolvedPath);
        Assert.Equal("RPATH", result.Source);
    }

    [Fact]
    public void Resolve_RpathPrecedesRunpath_WhenBothCouldMatch()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfo("/opt/erp/rpathlib/libfoo.so");
        fs.SetFileInfo("/opt/erp/runpathlib/libfoo.so");

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", null, ["/opt/erp/rpathlib"], ["/opt/erp/runpathlib"], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal("RPATH", result.Source);
        Assert.Equal("/opt/erp/rpathlib/libfoo.so", result.ResolvedPath);
    }

    [Fact]
    public void Resolve_NotInRpath_FoundInRunpath()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfo("/opt/erp/runpathlib/libfoo.so");

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", null, ["/opt/erp/rpathlib"], ["/opt/erp/runpathlib"], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.Resolved, result.Status);
        Assert.Equal("RUNPATH", result.Source);
    }

    [Fact]
    public void Resolve_OriginToken_ExpandedToImportingBinaryDirectory()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfo("/opt/erp/lib/libfoo.so");

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", "/opt/erp/bin/erp", ["$ORIGIN/../lib"], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.Resolved, result.Status);
        Assert.Equal("/opt/erp/lib/libfoo.so", result.ResolvedPath);
    }

    [Fact]
    public void Resolve_PathTraversalAttempt_NeverEscapesRoot_NeverThrows()
    {
        var fs = new FakeFileSystemReader();
        // No file registered anywhere — the point is this must not throw and must not somehow
        // "succeed" by escaping to an unintended absolute path.
        var resolver = new LinuxLibraryResolver(fs);

        var result = resolver.Resolve("libfoo.so", "/opt/erp/bin/erp", ["../../../../../../../../etc"], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.NotFound, result.Status);
    }

    [Fact]
    public void Resolve_KnownBinaryPathsSingleMatch_ReturnsResolvedWithKnownBinarySource()
    {
        var fs = new FakeFileSystemReader();
        var known = new Dictionary<string, IReadOnlyList<string>> { ["libfoo.so"] = ["/opt/other-app/lib/libfoo.so"] };

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", null, [], [], known, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.Resolved, result.Status);
        Assert.Equal("KnownBinary", result.Source);
        Assert.Equal("/opt/other-app/lib/libfoo.so", result.ResolvedPath);
    }

    [Fact]
    public void Resolve_KnownBinaryPathsMultipleDistinctMatches_ReturnsAmbiguous_NeverArbitrarilyChoosesOne()
    {
        var fs = new FakeFileSystemReader();
        var known = new Dictionary<string, IReadOnlyList<string>>
        {
            ["libfoo.so"] = ["/opt/app1/lib/libfoo.so", "/opt/app2/lib/libfoo.so"]
        };

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", null, [], [], known, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Null(result.ResolvedPath);
    }

    [Fact]
    public void Resolve_FoundInWellKnownLocation_WhenNoOtherTierMatches()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfo("/usr/lib/x86_64-linux-gnu/libssl.so.3");

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libssl.so.3", null, [], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.Resolved, result.Status);
        Assert.Equal("WellKnownLocation", result.Source);
    }

    [Fact]
    public void Resolve_FoundOnlyViaLdconfig_WhenNoOtherTierMatches()
    {
        var fs = new FakeFileSystemReader();
        var ldconfig = new Dictionary<string, string> { ["libcustom.so.1"] = "/opt/vendor/libcustom.so.1" };

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libcustom.so.1", null, [], [], EmptyKnownBinaries, ldconfig);

        Assert.Equal(LibraryResolutionStatus.Resolved, result.Status);
        Assert.Equal("Ldconfig", result.Source);
        Assert.Equal("/opt/vendor/libcustom.so.1", result.ResolvedPath);
    }

    [Fact]
    public void Resolve_NotFoundAnywhere_ReturnsNotFound_NeverFabricatesAPath()
    {
        var fs = new FakeFileSystemReader();

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libvendor.so", null, [], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.NotFound, result.Status);
        Assert.Null(result.ResolvedPath);
    }

    [Fact]
    public void Resolve_AccessDeniedOnEveryCandidate_ReturnsAccessDenied_NotNotFound()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfoFailure("/opt/erp/lib/libfoo.so", ServerSleuth.Infrastructure.Common.OperationStatus.AccessDenied);

        var resolver = new LinuxLibraryResolver(fs);
        var result = resolver.Resolve("libfoo.so", null, ["/opt/erp/lib"], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(LibraryResolutionStatus.AccessDenied, result.Status);
    }

    [Fact]
    public void Resolve_SameInputsCalledTwice_ProducesDeterministicResult()
    {
        var fs = new FakeFileSystemReader();
        fs.SetFileInfo("/opt/erp/lib/libfoo.so");
        var resolver = new LinuxLibraryResolver(fs);

        var resultA = resolver.Resolve("libfoo.so", null, ["/opt/erp/lib"], [], EmptyKnownBinaries, EmptyLdconfig);
        var resultB = resolver.Resolve("libfoo.so", null, ["/opt/erp/lib"], [], EmptyKnownBinaries, EmptyLdconfig);

        Assert.Equal(resultA, resultB);
    }
}
