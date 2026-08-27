using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The only real <see cref="ISshSession"/> implementation — wraps Renci.SshNet's
/// <see cref="SshClient"/> (exec channel) and <see cref="SftpClient"/> (structured file
/// access), see skill.md (Phase 10D-2) §3, §5. Host-key verification is wired through
/// <see cref="SshClient.HostKeyReceived"/>: every connection attempt computes the SHA-256
/// fingerprint of the offered host key and asks the injected <see cref="IHostKeyVerifier"/> —
/// there is no code path that sets <c>e.CanTrust = true</c> unconditionally (mechanically
/// verified by <see cref="NoBlindHostTrustTests"/>).
/// </summary>
public sealed class SshNetSession : ISshSession
{
    private readonly SshConnectionOptions _options;
    private SshClient? _sshClient;
    private SftpClient? _sftpClient;

    public SshNetSession(SshConnectionOptions options)
    {
        _options = options;
    }

    public bool IsConnected => _sshClient?.IsConnected == true && _sftpClient?.IsConnected == true;

    public SshConnectResult Connect(CancellationToken cancellationToken)
    {
        var credential = _options.CredentialProvider.GetCredential(_options.Target);
        var connectionInfo = BuildConnectionInfo(_options, credential);

        var hostKeyRejected = false;
        void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
        {
            var fingerprint = Convert.ToHexString(SHA256.HashData(e.HostKey)).ToLowerInvariant();
            var verdict = _options.HostKeyVerifier.Verify(_options.Host, _options.Port, fingerprint);
            e.CanTrust = verdict == HostKeyVerificationResult.Trusted;
            hostKeyRejected = verdict != HostKeyVerificationResult.Trusted;
        }

        _sshClient = new SshClient(connectionInfo);
        _sshClient.HostKeyReceived += OnHostKeyReceived;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sshClient.Connect();
        }
        catch (SshConnectionException) when (hostKeyRejected)
        {
            return SshConnectResult.HostKeyRejected();
        }
        catch (SshAuthenticationException)
        {
            return SshConnectResult.AuthenticationFailed();
        }
        catch (Renci.SshNet.Common.SshOperationTimeoutException)
        {
            return SshConnectResult.TimedOut();
        }
        catch (OperationCanceledException)
        {
            return SshConnectResult.Cancelled();
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or SshConnectionException)
        {
            return SshConnectResult.Unreachable(ex.Message);
        }
        finally
        {
            _sshClient.HostKeyReceived -= OnHostKeyReceived;
        }

        if (hostKeyRejected)
        {
            _sshClient.Disconnect();
            return SshConnectResult.HostKeyRejected();
        }

        try
        {
            _sftpClient = new SftpClient(connectionInfo);
            _sftpClient.Connect();
        }
        catch (Exception ex)
        {
            return SshConnectResult.Unreachable(ex.Message);
        }

        return SshConnectResult.Ok();
    }

    public SshCommandExecutionResult ExecuteCommand(string commandLine, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_sshClient is null || !_sshClient.IsConnected)
        {
            return SshCommandExecutionResult.TransportUnavailable();
        }

        using var command = _sshClient.CreateCommand(commandLine);
        command.CommandTimeout = timeout;

        var executeTask = Task.Run(() =>
        {
            var output = command.Execute();
            return (Output: output, command.ExitStatus, command.Error);
        }, cancellationToken);

        try
        {
            if (!executeTask.Wait(timeout, cancellationToken))
            {
                TryCancelCommand(command);
                return SshCommandExecutionResult.TimedOut();
            }
        }
        catch (OperationCanceledException)
        {
            TryCancelCommand(command);
            return SshCommandExecutionResult.Cancelled();
        }

        var (output, exitStatus, error) = executeTask.Result;
        return SshCommandExecutionResult.Ok(exitStatus ?? 0, output, error);
    }

    private static void TryCancelCommand(SshCommand command)
    {
        try
        {
            command.CancelAsync();
        }
        catch
        {
            // Best-effort — the underlying channel is being disposed regardless, which
            // ultimately tears down the remote process on the server side.
        }
    }

    public bool SftpExists(string path) => _sftpClient?.Exists(path) == true;

    public FileSystemResult<byte[]> SftpReadBytes(string path)
    {
        if (_sftpClient is null)
        {
            return FileSystemResult<byte[]>.Failure(OperationStatus.TransportUnavailable, "Not connected.");
        }

        try
        {
            using var stream = new MemoryStream();
            _sftpClient.DownloadFile(path, stream);
            return FileSystemResult<byte[]>.Ok(stream.ToArray());
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<byte[]>.Failure(status, ex.Message);
        }
    }

    public FileSystemResult<SshRemoteFileInfo> SftpGetAttributes(string path)
    {
        if (_sftpClient is null)
        {
            return FileSystemResult<SshRemoteFileInfo>.Failure(OperationStatus.TransportUnavailable, "Not connected.");
        }

        try
        {
            var file = _sftpClient.Get(path);
            return FileSystemResult<SshRemoteFileInfo>.Ok(ToRemoteFileInfo(file));
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<SshRemoteFileInfo>.Failure(status, ex.Message);
        }
    }

    /// <summary>
    /// Known limitation (skill.md Phase 10D-2 §9-10, documented rather than hacked around):
    /// Renci.SshNet's public <see cref="SftpClient"/> surface in the referenced version exposes
    /// no SSH_FXP_READLINK operation — only the write-direction
    /// <c>SymbolicLink(path, linkPath)</c> (creates a link) exists, nothing reads one. Falls back
    /// to the single well-known, read-only POSIX <c>readlink</c> utility, invoked through the
    /// exact same safely-quoted <see cref="ExecuteCommand"/> path as any other
    /// <see cref="RemoteOperationKind.ProcessQuery"/> — the same class of narrow, named,
    /// read-only-utility exception this codebase already accepts for <c>ldconfig -p</c>
    /// (native dependency discovery) and <c>systemctl show</c> (systemd discovery). This is
    /// never a raw/opaque shell string — <paramref name="path"/> goes through
    /// <see cref="SshCommandLineBuilder.Build"/> exactly like any other argument.
    /// </summary>
    public FileSystemResult<string> ReadLinkTarget(string path)
    {
        var commandLine = SshCommandLineBuilder.Build("readlink", [path]);
        var result = ExecuteCommand(commandLine, TimeSpan.FromSeconds(10), CancellationToken.None);

        if (result.Status != OperationStatus.Success)
        {
            return FileSystemResult<string>.Failure(result.Status, "readlink failed.");
        }

        var target = result.StandardOutput.TrimEnd('\n', '\r');
        return target.Length == 0
            ? FileSystemResult<string>.Failure(OperationStatus.NotFound, $"'{path}' is not a symbolic link.")
            : FileSystemResult<string>.Ok(target);
    }

    public FileSystemResult<IReadOnlyList<SshRemoteFileInfo>> SftpListDirectory(string path)
    {
        if (_sftpClient is null)
        {
            return FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Failure(OperationStatus.TransportUnavailable, "Not connected.");
        }

        try
        {
            IReadOnlyList<SshRemoteFileInfo> entries = _sftpClient.ListDirectory(path)
                .Where(f => f.Name != "." && f.Name != "..")
                .Select(ToRemoteFileInfo)
                .ToList();

            return FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Ok(entries);
        }
        catch (Exception ex) when (TryClassify(ex, out var status))
        {
            return FileSystemResult<IReadOnlyList<SshRemoteFileInfo>>.Failure(status, ex.Message);
        }
    }

    private static SshRemoteFileInfo ToRemoteFileInfo(Renci.SshNet.Sftp.ISftpFile file) => new()
    {
        FullPath = file.FullName,
        SizeBytes = file.Attributes.Size,
        LastWriteTimeUtc = file.Attributes.LastWriteTimeUtc,
        IsDirectory = file.IsDirectory,
        IsSymbolicLink = file.IsSymbolicLink
    };

    private static bool TryClassify(Exception ex, out OperationStatus status)
    {
        status = ex switch
        {
            SftpPermissionDeniedException => OperationStatus.AccessDenied,
            SftpPathNotFoundException => OperationStatus.NotFound,
            SshConnectionException => OperationStatus.TransportUnavailable,
            _ => OperationStatus.IoError
        };

        return ex is SftpPermissionDeniedException or SftpPathNotFoundException or SshConnectionException or IOException;
    }

    private static ConnectionInfo BuildConnectionInfo(SshConnectionOptions options, RemoteCredential credential)
    {
        var authMethods = new List<AuthenticationMethod>();

        if (credential.PrivateKeyBytes is not null)
        {
            using var keyStream = new MemoryStream(credential.PrivateKeyBytes);
            var keyFile = credential.PrivateKeyPassphrase is null
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, credential.PrivateKeyPassphrase);

            authMethods.Add(new PrivateKeyAuthenticationMethod(credential.Username, keyFile));
        }

        if (credential.Password is not null)
        {
            authMethods.Add(new PasswordAuthenticationMethod(credential.Username, credential.Password));
        }

        var connectionInfo = new ConnectionInfo(options.Host, options.Port, credential.Username, [.. authMethods])
        {
            Timeout = options.ConnectTimeout
        };

        return connectionInfo;
    }

    public void Dispose()
    {
        _sftpClient?.Dispose();
        _sshClient?.Dispose();
    }
}
