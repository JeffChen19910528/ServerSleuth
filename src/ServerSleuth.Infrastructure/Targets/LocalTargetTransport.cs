using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Infrastructure.Targets;

/// <summary>
/// The only <see cref="ITargetTransport"/> implementation that exists — makes local scanning
/// explicitly a target rather than an implicit default (skill.md Phase 10C §3), by wrapping the
/// SAME already-registered local <see cref="IProcessRunner"/>/<see cref="IFileSystemReader"/>
/// singletons every scanner already uses via direct DI injection. Registering this changes
/// nothing about how scanners run — they still receive <see cref="IProcessRunner"/>/
/// <see cref="IFileSystemReader"/> directly, unchanged from every prior phase. This type carries
/// no socket, no remote credential, no SSH/WinRM client of any kind (skill.md §4, §6, §8) — it
/// is a plain, local, in-process pass-through.
/// </summary>
public sealed class LocalTargetTransport(ScanTarget target, IProcessRunner processRunner, IFileSystemReader fileSystemReader) : ITargetTransport
{
    public ScanTarget Target { get; } = target;
    public IProcessRunner ProcessRunner { get; } = processRunner;
    public IFileSystemReader FileSystemReader { get; } = fileSystemReader;
}
