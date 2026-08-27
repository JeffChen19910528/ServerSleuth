namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// Everything an <see cref="ISshSession"/> needs to attempt one connection — see skill.md
/// (Phase 10D-2) §6, §11, §25. Carries an <see cref="IRemoteCredentialProvider"/>/
/// <see cref="IHostKeyVerifier"/> REFERENCE, never a raw credential/fingerprint value itself, so
/// this options object stays safe to pass around and log-describe (host/port/username only — see
/// <see cref="Targets.RemoteOperation.DescribeForLogging"/> for the equivalent pattern).
/// </summary>
public sealed record SshConnectionOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 22;
    public required IRemoteCredentialProvider CredentialProvider { get; init; }
    public required IHostKeyVerifier HostKeyVerifier { get; init; }

    /// <summary>How long establishing the TCP+SSH handshake itself may take — independent of any
    /// individual operation's own timeout below (skill.md §25: "do not create one giant global
    /// SSH timeout that blocks every operation").</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>The remote <see cref="Core.Targets.ScanTarget"/> this connection serves — used
    /// only to hand back to <see cref="IRemoteCredentialProvider.GetCredential"/>, never for
    /// anything address-shaped (that's <see cref="Host"/>/<see cref="Port"/>).</summary>
    public required Core.Targets.ScanTarget Target { get; init; }
}
