using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Windows.Binaries;

namespace ServerSleuth.Windows.Tests.Binaries;

public class BoundedDirectoryWalkerTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemReader _fileSystemReader = new();

    public BoundedDirectoryWalkerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "serversleuth-walker-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Walk_FilesInRootAndSubdirectory_AreAllFound()
    {
        File.WriteAllText(Path.Combine(_root, "a.dll"), "");
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "b.dll"), "");

        var result = BoundedDirectoryWalker.Walk(_fileSystemReader, _root, ["*.dll"]);

        Assert.Equal(2, result.Files.Count);
        Assert.False(result.DepthLimitReached);
        Assert.False(result.FileLimitReached);
    }

    [Fact]
    public void Walk_OnlyMatchingExtensions_AreReturned()
    {
        File.WriteAllText(Path.Combine(_root, "a.dll"), "");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "");

        var result = BoundedDirectoryWalker.Walk(_fileSystemReader, _root, ["*.dll"]);

        Assert.Single(result.Files);
        Assert.EndsWith(".dll", result.Files[0]);
    }

    [Fact]
    public void Walk_DepthExceedingLimit_StopsAndReportsDepthLimitReached()
    {
        var current = _root;
        for (var i = 0; i < 5; i++)
        {
            current = Path.Combine(current, $"d{i}");
            Directory.CreateDirectory(current);
        }
        File.WriteAllText(Path.Combine(current, "deep.dll"), "");

        var result = BoundedDirectoryWalker.Walk(_fileSystemReader, _root, ["*.dll"], maxDepth: 2, maxFiles: 100);

        Assert.True(result.DepthLimitReached);
        Assert.Empty(result.Files); // the file is beyond the depth limit
    }

    [Fact]
    public void Walk_FileCountExceedingLimit_StopsAndReportsFileLimitReached()
    {
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_root, $"file{i}.dll"), "");
        }

        var result = BoundedDirectoryWalker.Walk(_fileSystemReader, _root, ["*.dll"], maxDepth: 8, maxFiles: 3);

        Assert.True(result.FileLimitReached);
        Assert.True(result.Files.Count <= 3);
    }

    [Fact]
    public void Walk_EmptyRoot_ReturnsNoFilesNoLimitsReached()
    {
        var result = BoundedDirectoryWalker.Walk(_fileSystemReader, _root, ["*.dll"]);

        Assert.Empty(result.Files);
        Assert.False(result.DepthLimitReached);
        Assert.False(result.FileLimitReached);
        Assert.Equal(0, result.ReparsePointsSkipped);
    }

    [Fact]
    public void Walk_NonExistentRoot_ReturnsEmptyWithoutThrowing()
    {
        var result = BoundedDirectoryWalker.Walk(_fileSystemReader, Path.Combine(_root, "does-not-exist"), ["*.dll"]);

        Assert.Empty(result.Files);
    }
}
