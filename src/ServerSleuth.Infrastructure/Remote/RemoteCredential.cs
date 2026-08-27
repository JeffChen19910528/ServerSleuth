namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// The minimum SSH authentication material an <see cref="ISshSession"/> needs to connect — see
/// skill.md (Phase 10D-2) §4. Deliberately NOT a property of <see cref="Core.Targets.ScanTarget"/>,
/// <see cref="Core.Interfaces.DiscoveryContext"/>, or any domain/report model — it exists only in
/// memory, only for the duration of one connection attempt, supplied by an
/// <see cref="IRemoteCredentialProvider"/> at connect time. Private-key authentication is the
/// preferred, fully-supported path (skill.md §4: "Prefer SSH private key"); password
/// authentication is supported only because some environments have no other option, never as the
/// primary/only path.
///
/// Never serialized, never logged: a plain <c>sealed record</c>'s COMPILER-GENERATED
/// <c>ToString()</c> would otherwise print every property's raw value (records, unlike classes,
/// print field values by default) — a genuine secret-logging trap for a type holding a password/
/// passphrase/key. <see cref="ToString"/> is therefore explicitly overridden below to print
/// nothing but the username, mechanically verified by a reflection/behavior test. Nothing in this
/// codebase logs an instance of this type — see <see cref="RemoteOperation.DescribeForLogging"/>
/// for the pattern actually used for diagnostics instead.
/// </summary>
public sealed record RemoteCredential
{
    public required string Username { get; init; }

    /// <summary>Raw PEM/OpenSSH private key bytes — never a file path (the caller reads the
    /// file; this type never touches disk itself). Mutually exclusive with <see cref="Password"/>.</summary>
    public byte[]? PrivateKeyBytes { get; init; }

    /// <summary>Optional passphrase protecting <see cref="PrivateKeyBytes"/>.</summary>
    public string? PrivateKeyPassphrase { get; init; }

    /// <summary>Password authentication — supported only when no private key is available
    /// (skill.md §4: "Password authentication may be supported only if necessary").</summary>
    public string? Password { get; init; }

    public static RemoteCredential ForPrivateKey(string username, byte[] privateKeyBytes, string? passphrase = null) => new()
    {
        Username = username,
        PrivateKeyBytes = privateKeyBytes,
        PrivateKeyPassphrase = passphrase
    };

    public static RemoteCredential ForPassword(string username, string password) => new()
    {
        Username = username,
        Password = password
    };

    /// <summary>Overrides the compiler-generated record <c>ToString()</c> so an accidental
    /// <c>logger.Log(credential)</c>/string-interpolation call can never print a secret.</summary>
    public override string ToString() => $"RemoteCredential {{ Username = {Username} }}";
}
