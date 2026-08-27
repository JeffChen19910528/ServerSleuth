using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Cli.Tests.Fakes;

/// <summary>
/// A minimal <see cref="ITargetTransport"/> double carrying a <see cref="ScanTarget.Kind"/> of
/// <see cref="TargetKind.Remote"/> — Phase 10E-1's pipeline-integration tests use this (never a
/// real <c>SshRemoteTargetTransport</c>/<c>WindowsRemoteTargetTransport</c>) so
/// <c>ScanCommand</c>'s connect branch is skipped (neither concrete type matches) and the SAME
/// downstream pipeline that processes a local scan processes this one — proving the pipeline
/// itself is target-agnostic, without re-testing real SSH/WinRM connect semantics already
/// covered by Phase 10D-2/10D-3B's own fake-session suites.
///
/// <see cref="ProcessRunner"/>/<see cref="FileSystemReader"/> both throw if ever actually
/// invoked — nothing in a fake-discovery-engine-driven pipeline test should call either.
/// </summary>
internal sealed class FakeRemoteTargetTransport(ScanTarget target) : ITargetTransport
{
    public ScanTarget Target { get; } = target;
    public IProcessRunner ProcessRunner { get; } = new NeverCalledProcessRunner();
    public IFileSystemReader FileSystemReader { get; } = new NeverCalledFileSystemReader();

    private sealed class NeverCalledProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A Phase 10E-1 pipeline-integration test's fake transport should never have its ProcessRunner invoked.");
    }

    private sealed class NeverCalledFileSystemReader : IFileSystemReader
    {
        private static InvalidOperationException NotExpected() =>
            new("A Phase 10E-1 pipeline-integration test's fake transport should never have its FileSystemReader invoked.");

        public bool Exists(string path) => throw NotExpected();
        public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken) => throw NotExpected();
        public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken) => throw NotExpected();
        public FileSystemResult<FileEntryInfo> GetFileInfo(string path) => throw NotExpected();
        public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false) => throw NotExpected();
        public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false) => throw NotExpected();
        public FileSystemResult<string> ReadLinkTarget(string path) => throw NotExpected();
    }
}
