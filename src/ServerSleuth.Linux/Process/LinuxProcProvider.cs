using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;

namespace ServerSleuth.Linux.Process;

/// <summary>
/// Real `/proc` reader. Enumerates numeric entries under `/proc` (never any other directory —
/// skill.md (Phase 6A) §11) and reads `status`/`cmdline` via the shared, permission-safe
/// <see cref="IFileSystemReader"/>. The `exe` symlink is resolved via
/// <see cref="FileInfo.LinkTarget"/> (a single, targeted read of one known-shape special file,
/// not a filesystem walk) — a process that has exited, is a zombie, or is a kernel thread
/// simply yields a null target rather than an exception. See skill.md §4.
/// </summary>
public sealed class LinuxProcProvider(IFileSystemReader fileSystemReader) : IProcProvider
{
    public IReadOnlyList<ProcProcessSnapshot> GetProcessSnapshots()
    {
        var directoriesResult = fileSystemReader.EnumerateDirectories("/proc", "*");
        if (!directoriesResult.Success)
        {
            return [];
        }

        var snapshots = new List<ProcProcessSnapshot>();

        foreach (var directory in directoriesResult.Value!)
        {
            var name = Path.GetFileName(directory.TrimEnd('/'));
            if (!int.TryParse(name, out var pid))
            {
                continue; // "self", "curproc", "sys", etc. — not a process entry
            }

            snapshots.Add(ReadProcess(pid));
        }

        return snapshots;
    }

    private ProcProcessSnapshot ReadProcess(int pid)
    {
        var statusResult = fileSystemReader.ReadTextAsync($"/proc/{pid}/status", CancellationToken.None).GetAwaiter().GetResult();
        if (!statusResult.Success)
        {
            return new ProcProcessSnapshot { Pid = pid, AccessDenied = statusResult.Status == OperationStatus.AccessDenied };
        }

        var fields = ProcStatusParser.Parse(statusResult.Value!);
        var name = fields.GetValueOrDefault("Name");

        if (name is null)
        {
            return new ProcProcessSnapshot { Pid = pid, MalformedEntry = true };
        }

        var ppid = int.TryParse(fields.GetValueOrDefault("PPid"), out var parsedPpid) ? parsedPpid : (int?)null;
        var uid = ProcStatusParser.ExtractRealUid(fields.GetValueOrDefault("Uid"));

        var cmdlineResult = fileSystemReader.ReadTextAsync($"/proc/{pid}/cmdline", CancellationToken.None).GetAwaiter().GetResult();
        string? commandLine = null;
        if (cmdlineResult.Success)
        {
            var joined = string.Join(' ', cmdlineResult.Value!.Split('\0', StringSplitOptions.RemoveEmptyEntries));
            commandLine = joined.Length > 0 ? joined : null;
        }

        return new ProcProcessSnapshot
        {
            Pid = pid,
            ParentPid = ppid,
            Name = name,
            State = fields.GetValueOrDefault("State"),
            CommandLine = commandLine,
            ExecutablePath = ResolveExeTarget(pid),
            Uid = uid
        };
    }

    /// <summary>Phase 10D-2: routed through <see cref="IFileSystemReader.ReadLinkTarget"/>
    /// instead of calling <c>FileInfo.LinkTarget</c> directly on the local disk — the previous
    /// implementation would have silently resolved the LOCAL machine's <c>/proc</c> even when
    /// scanning a remote target. Access denied, process exited between listing and this read,
    /// zombie, or kernel thread (no exe target at all) — all normal, never fatal to the scan; a
    /// non-success result is simply treated as "no target."</summary>
    private string? ResolveExeTarget(int pid)
    {
        var result = fileSystemReader.ReadLinkTarget($"/proc/{pid}/exe");
        return result.Success ? result.Value : null;
    }
}
