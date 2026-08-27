namespace ServerSleuth.Core.Targets;

/// <summary>
/// What kind of machine a <see cref="ScanTarget"/> refers to. Only <see cref="Local"/> is
/// actually scannable today — <see cref="Remote"/> exists so the target model, CLI option
/// surface, and future transport boundary have a well-defined shape to grow into, without any
/// remote transport (SSH/WinRM/etc.) being implemented. See skill.md (Phase 10C) §2, §5, §11.
/// </summary>
public enum TargetKind
{
    Local,
    Remote
}
