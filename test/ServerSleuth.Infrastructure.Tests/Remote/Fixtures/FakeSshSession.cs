using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Remote;

namespace ServerSleuth.Infrastructure.Tests.Remote.Fixtures;

/// <summary>Deterministic in-memory <see cref="ISshSession"/> double — see skill.md
/// (Phase 10D-2) §27: no live SSH server is needed for the normal test suite.</summary>
public sealed class FakeSshSession : ISshSession
{
    private readonly Dictionary<string, SshCommandExecutionResult> _commandResults = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SshRemoteFileInfo> _attributes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<SshRemoteFileInfo>> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _linkTargets = new(StringComparer.Ordinal);

    public SshConnectResult ConnectResult { get; set; } = SshConnectResult.Ok();
    public bool IsConnected { get; set; }
    public string? LastExecutedCommandLine { get; private set; }
    public int ConnectCallCount { get; private set; }
    public int DisposeCallCount { get; private set; }

    public void SetCommandResult(string commandLine, SshCommandExecutionResult result) => _commandResults[commandLine] = result;

    public void SetFile(string path, byte[] content) => _files[path] = content;

    public void SetAttributes(string path, SshRemoteFileInfo info) => _attributes[path] = info;

    public void SetDirectory(string path, IReadOnlyList<SshRemoteFileInfo> entries) => _directories[path] = entries;

    public void SetLinkTarget(string path, string target) => _linkTargets[path] = target;

    public SshConnectResult Connect(CancellationToken cancellationToken)
    {
        ConnectCallCount++;
        if (ConnectResult.Success)
        {
            IsConnected = true;
        }

        return ConnectResult;
    }

    public SshCommandExecutionResult ExecuteCommand(string commandLine, TimeSpan timeout, CancellationToken cancellationToken)
    {
        LastExecutedCommandLine = commandLine;

        if (!IsConnected)
        {
            return SshCommandExecutionResult.TransportUnavailable();
        }

        return _commandResults.TryGetValue(commandLine, out var result)
            ? result
            : SshCommandExecutionResult.Ok(0, string.Empty, string.Empty);
    }

    public bool SftpExists(string path) => _files.ContainsKey(path) || _attributes.ContainsKey(path);

    public FileSystemResult<byte[]> SftpReadBytes(string path) =>
        _files.TryGetValue(path, out var bytes)
            ? FileSystemResult<byte[]>.Ok(bytes)
            : FileSystemResult<byte[]>.Failure(OperationStatus.NotFound, "not found");

    public FileSystemResult<SshRemoteFileInfo> SftpGetAttributes(string path) =>
        _attributes.TryGetValue(path, out var info)
            ? FileSystemResult<SshRemoteFileInfo>.Ok(info)
            : FileSystemResult<SshRemoteFileInfo>.Failure(OperationStatus.NotFound, "not found");

    public FileSystemResult<string> ReadLinkTarget(string path) =>
        _linkTargets.TryGetValue(path, out var target)
            ? FileSystemResult<string>.Ok(target)
            : FileSystemResult<string>.Failure(OperationStatus.NotFound, "not a symlink");

    public FileSystemResult<IReadOnlyList<SshRemoteFileInfo>> SftpListDirectory(string path) =>
        _directories.TryGetValue(path, out var entries)
            ? FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Ok(entries)
            : FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Failure(OperationStatus.NotFound, "not found");

    public void Dispose()
    {
        DisposeCallCount++;
        IsConnected = false;
    }
}
