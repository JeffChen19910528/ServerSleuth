using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Gui.ExecutionHost.Tests.Fakes;

/// <summary>A plain, non-SSH/non-WinRM <see cref="ITargetTransport"/> double — mirrors
/// <c>ServerSleuth.Cli.Tests.Fakes.TestServiceProviderFactory</c>'s own reasoning for why this
/// is safe: <see cref="GuiScanExecutor"/>'s connect branch only pattern-matches
/// <c>SshRemoteTargetTransport</c>/<c>WindowsRemoteTargetTransport</c>, so a double of this
/// shape skips straight to discovery, exactly like the fake <see cref="FakeDiscoveryEngine"/>
/// already does for the discovery stage itself. Never touches a real process/filesystem.</summary>
internal sealed class FakeTargetTransport(ScanTarget target) : ITargetTransport
{
    public ScanTarget Target { get; } = target;
    public IProcessRunner ProcessRunner { get; } = new ThrowingProcessRunner();
    public IFileSystemReader FileSystemReader { get; } = new ThrowingFileSystemReader();

    private sealed class ThrowingProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The fake discovery engine should never call the process runner.");
    }

    private sealed class ThrowingFileSystemReader : IFileSystemReader
    {
        public bool Exists(string path) => throw new InvalidOperationException("The fake discovery engine should never call the filesystem reader.");
        public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public FileSystemResult<FileEntryInfo> GetFileInfo(string path) => throw new InvalidOperationException();
        public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false) =>
            throw new InvalidOperationException();
        public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false) =>
            throw new InvalidOperationException();
        public FileSystemResult<string> ReadLinkTarget(string path) => throw new InvalidOperationException();
    }
}
