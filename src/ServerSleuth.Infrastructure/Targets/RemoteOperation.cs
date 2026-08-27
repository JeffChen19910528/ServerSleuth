using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// A structured discovery operation a future remote transport would carry to a
/// <see cref="ScanTarget"/> — see skill.md (Phase 10D-1) §3. Deliberately NOT a
/// <c>string command</c>/<c>Execute(string)</c>/<c>RunShell(string)</c> shape: it preserves
/// executable identity, structured arguments, timeout, target, and an explicit
/// <see cref="RemoteOperationKind"/> classification as separate, typed fields — never a single
/// opaque string a transport would have to parse or interpret. Cancellation is deliberately NOT
/// a field here (a <see cref="CancellationToken"/> cannot be meaningfully stored in an immutable
/// record); it is supplied at the point an operation is actually executed, exactly like
/// <see cref="Process.IProcessRunner.RunAsync"/> already does.
///
/// This type is data only — nothing in this codebase executes a <see cref="RemoteOperation"/>
/// yet (skill.md §3, §26: no actual remote execution in this phase). It exists so a future
/// SSH/WinRM transport implementation has an already-defined, injection-safe shape to accept.
/// </summary>
public sealed record RemoteOperation
{
    public required ScanTarget Target { get; init; }
    public required RemoteOperationKind Kind { get; init; }

    /// <summary>Set only for <see cref="RemoteOperationKind.ProcessQuery"/>.</summary>
    public string? Executable { get; init; }

    /// <summary>Set only for <see cref="RemoteOperationKind.ProcessQuery"/> — kept as discrete
    /// argv-style entries, never concatenated into a single string, exactly like
    /// <see cref="Process.ProcessRequest.Arguments"/>.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Set only for <see cref="RemoteOperationKind.FileRead"/>/<see cref="RemoteOperationKind.DirectoryQuery"/>.</summary>
    public string? Path { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public static RemoteOperation ForProcessQuery(
        ScanTarget target, string executable, IReadOnlyList<string>? arguments = null, TimeSpan? timeout = null) => new()
    {
        Target = target,
        Kind = RemoteOperationKind.ProcessQuery,
        Executable = executable,
        Arguments = arguments ?? [],
        Timeout = timeout ?? TimeSpan.FromSeconds(30)
    };

    public static RemoteOperation ForFileRead(ScanTarget target, string path, TimeSpan? timeout = null) => new()
    {
        Target = target,
        Kind = RemoteOperationKind.FileRead,
        Path = path,
        Timeout = timeout ?? TimeSpan.FromSeconds(30)
    };

    public static RemoteOperation ForDirectoryQuery(ScanTarget target, string path, TimeSpan? timeout = null) => new()
    {
        Target = target,
        Kind = RemoteOperationKind.DirectoryQuery,
        Path = path,
        Timeout = timeout ?? TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// A safe-for-logging, single-line representation — see skill.md (Phase 10D-1) §8. Every
    /// value that could ever carry a secret (target identity, executable, each argument, path)
    /// is passed through the SAME <see cref="ISecretRedactor"/> every discovered configuration
    /// value already goes through, so a future transport's diagnostic logging can never leak a
    /// credential embedded in an argument or path without a separate redaction mechanism being
    /// invented for it later.
    /// </summary>
    public string DescribeForLogging(ISecretRedactor redactor)
    {
        var targetId = redactor.Redact(Target.Id);

        return Kind switch
        {
            RemoteOperationKind.ProcessQuery =>
                $"{Kind} target={targetId} executable={redactor.Redact(Executable ?? string.Empty)} " +
                $"args=[{string.Join(' ', Arguments.Select(redactor.Redact))}] timeout={Timeout}",

            _ =>
                $"{Kind} target={targetId} path={redactor.Redact(Path ?? string.Empty)} timeout={Timeout}"
        };
    }
}
