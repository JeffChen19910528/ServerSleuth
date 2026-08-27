using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// The boundary between a <see cref="ScanTarget"/> and the structured, already-transport-
/// agnostic operations (<see cref="IProcessRunner"/>/<see cref="IFileSystemReader"/> — both
/// already request/result-based, never a raw shell command string) that actually reach it — see
/// skill.md (Phase 10C) §5-7. <see cref="Core.Targets.TargetKind.Local"/> execution is
/// transport-agnostic by nature (plain in-process calls); this interface exists purely so a
/// FUTURE remote transport (SSH/WinRM/etc. — none implemented anywhere in this codebase) has a
/// well-defined shape to implement, without <c>IDiscoveryScanner</c>/<c>DiscoveryEngine</c>, or
/// any scanner, ever depending on a specific transport or a generic
/// <c>Execute(string command)</c>-style API (skill.md §6). Only <see cref="IProcessRunner"/>/
/// <see cref="IFileSystemReader"/> are exposed here — <c>IPortInspector</c> remains registered
/// per-platform (Windows/Linux each supply their own) rather than through this cross-platform
/// boundary, since no cross-platform default implementation of it exists to wrap (documented as
/// a known limitation, not an oversight).
/// </summary>
public interface ITargetTransport
{
    ScanTarget Target { get; }
    IProcessRunner ProcessRunner { get; }
    IFileSystemReader FileSystemReader { get; }
}
