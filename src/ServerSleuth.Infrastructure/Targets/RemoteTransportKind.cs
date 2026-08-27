namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// Which remote transport protocol a <see cref="Core.Targets.TargetPlatform"/> would use — see
/// skill.md (Phase 10D-1) §10-12. Purely descriptive: nothing in this codebase constructs an
/// SSH or WinRM connection anywhere (skill.md §21, §26). Exists so
/// <see cref="RemoteTargetTransportFactory"/> can state, structurally, which future transport a
/// given platform maps to, rather than encoding that mapping only in a comment.
/// </summary>
public enum RemoteTransportKind
{
    /// <summary>Future transport for <see cref="Core.Targets.TargetPlatform.Linux"/> remote
    /// targets. No SSH library (e.g. SSH.NET) is referenced anywhere in this codebase.</summary>
    Ssh,

    /// <summary>Future transport for <see cref="Core.Targets.TargetPlatform.Windows"/> remote
    /// targets. No WinRM/PowerShell-Remoting package is referenced anywhere in this
    /// codebase.</summary>
    WinRm
}
