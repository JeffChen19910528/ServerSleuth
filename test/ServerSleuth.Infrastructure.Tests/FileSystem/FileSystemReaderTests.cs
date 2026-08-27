using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Infrastructure.Tests.FileSystem;

public class FileSystemReaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemReader _reader = new();

    public FileSystemReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "servesleuth-fs-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Exists_ReturnsTrueForFileAndDirectory()
    {
        var filePath = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(filePath, "content");

        Assert.True(_reader.Exists(filePath));
        Assert.True(_reader.Exists(_tempDir));
        Assert.False(_reader.Exists(Path.Combine(_tempDir, "does-not-exist.txt")));
    }

    [Fact]
    public async Task ReadTextAsync_ReturnsFileContent()
    {
        var filePath = Path.Combine(_tempDir, "text.txt");
        await File.WriteAllTextAsync(filePath, "hello evidence");

        var result = await _reader.ReadTextAsync(filePath, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("hello evidence", result.Value);
    }

    [Fact]
    public async Task ReadTextAsync_MissingFile_ReturnsNotFound()
    {
        var filePath = Path.Combine(_tempDir, "missing.txt");

        var result = await _reader.ReadTextAsync(filePath, CancellationToken.None);

        Assert.Equal(OperationStatus.NotFound, result.Status);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ReadTextAsync_PathIsDirectory_ReturnsAccessDenied()
    {
        var result = await _reader.ReadTextAsync(_tempDir, CancellationToken.None);

        Assert.Equal(OperationStatus.AccessDenied, result.Status);
    }

    [Fact]
    public async Task ReadBytesAsync_ReturnsFileBytes()
    {
        var filePath = Path.Combine(_tempDir, "bytes.bin");
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(filePath, expected);

        var result = await _reader.ReadBytesAsync(filePath, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ReadBytesAsync_FileLockedByAnotherProcess_ReturnsIoError()
    {
        var filePath = Path.Combine(_tempDir, "locked.bin");
        await File.WriteAllBytesAsync(filePath, [1, 2, 3]);

        using var lockingStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await _reader.ReadBytesAsync(filePath, CancellationToken.None);

        Assert.Equal(OperationStatus.IoError, result.Status);
    }

    [Fact]
    public void GetFileInfo_ExistingFile_ReturnsSizeAndPath()
    {
        var filePath = Path.Combine(_tempDir, "info.txt");
        File.WriteAllText(filePath, "12345");

        var result = _reader.GetFileInfo(filePath);

        Assert.True(result.Success);
        Assert.Equal(5, result.Value!.SizeBytes);
        Assert.False(result.Value.IsDirectory);
    }

    [Fact]
    public void GetFileInfo_Directory_ReturnsIsDirectoryTrue()
    {
        var result = _reader.GetFileInfo(_tempDir);

        Assert.True(result.Success);
        Assert.True(result.Value!.IsDirectory);
    }

    [Fact]
    public void GetFileInfo_MissingPath_ReturnsNotFound()
    {
        var result = _reader.GetFileInfo(Path.Combine(_tempDir, "nope.txt"));

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public void EnumerateFiles_ReturnsCreatedFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "one.txt"), "1");
        File.WriteAllText(Path.Combine(_tempDir, "two.txt"), "2");

        var result = _reader.EnumerateFiles(_tempDir, "*.txt");

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public void EnumerateFiles_MissingDirectory_ReturnsNotFound()
    {
        var result = _reader.EnumerateFiles(Path.Combine(_tempDir, "no-such-dir"));

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public void EnumerateDirectories_ReturnsSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub-a"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub-b"));

        var result = _reader.EnumerateDirectories(_tempDir);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
    }
}
