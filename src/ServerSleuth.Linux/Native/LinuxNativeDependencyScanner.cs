using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Linux.Common;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Runtimes;
using ServerSleuth.Linux.Systemd;
using CoreDll = ServerSleuth.Core.Models.Dll;
using CoreProcess = ServerSleuth.Core.Models.Process;

namespace ServerSleuth.Linux.Native;

/// <summary>
/// Discovers native Linux (ELF) binary dependencies from already-discovered executable
/// evidence only — never a filesystem-wide ELF search. Reuses LinuxProcessScanner/
/// LinuxSystemdServiceScanner/LinuxScheduledTaskScanner/LinuxRuntimeDiscoveryScanner directly
/// (re-running them, not duplicating their logic) purely to obtain executable paths, exactly
/// as WindowsBinaryDiscoveryScanner reuses IIS/Service/ScheduledTask/COM discovery. See
/// skill.md (Phase 6F) §1-2.
/// </summary>
public sealed class LinuxNativeDependencyScanner(
    LinuxProcessScanner processScanner,
    LinuxSystemdServiceScanner systemdScanner,
    LinuxScheduledTaskScanner scheduledTaskScanner,
    LinuxRuntimeDiscoveryScanner runtimeScanner,
    IFileSystemReader fileSystemReader,
    ILinuxElfParser elfParser,
    ILibraryResolver libraryResolver,
    ILdconfigProvider ldconfigProvider,
    ILogger<LinuxNativeDependencyScanner> logger) : IDiscoveryScanner
{
    // Generous relative to real ELF binaries (nearly all of which are well under this) — bounds
    // memory usage against a pathological input rather than reflecting any real Linux binary
    // size norm. See skill.md (Phase 6F) §27.
    private const long MaxAnalysisSizeBytes = 200L * 1024 * 1024;

    public string Id => "linux-native-dependency-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var processResult = await processScanner.ScanAsync(context, cancellationToken);
        var systemdResult = await systemdScanner.ScanAsync(context, cancellationToken);
        var taskResult = await scheduledTaskScanner.ScanAsync(context, cancellationToken);
        var runtimeResult = await runtimeScanner.ScanAsync(context, cancellationToken);

        var pathToOwners = CollectOwnedPaths(processResult, systemdResult, taskResult, runtimeResult);

        var ldconfigCache = await ldconfigProvider.GetCacheAsync(cancellationToken);

        var errors = new List<DiscoveryError>();
        var rows = new List<NativeBinaryRow>();

        foreach (var (path, owners) in pathToOwners)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(await AnalyzeBinary(path, owners, ldconfigCache, errors));
        }

        // Known-binary-path resolution tier needs every row's path up front, so dependency
        // resolution happens in a second pass over the now-complete row set.
        var knownBinaryPathsByFileName = BuildKnownBinaryIndex(rows);
        var resolvedRows = rows.Select(row => ResolveDependencies(row, knownBinaryPathsByFileName, ldconfigCache)).ToList();

        var entities = resolvedRows.Select(BuildEntity).ToList();

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} native binaries", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    private static Dictionary<string, List<string>> CollectOwnedPaths(
        DiscoveryResult processResult, DiscoveryResult systemdResult, DiscoveryResult taskResult, DiscoveryResult runtimeResult)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal); // Linux paths are case-sensitive

        void Add(string? path, string ownerId)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith('/'))
            {
                return;
            }

            if (!map.TryGetValue(path, out var owners))
            {
                owners = [];
                map[path] = owners;
            }

            if (!owners.Contains(ownerId, StringComparer.Ordinal))
            {
                owners.Add(ownerId);
            }
        }

        foreach (var process in processResult.Entities.OfType<CoreProcess>())
        {
            Add(process.Path, process.Id);
        }

        foreach (var service in systemdResult.Entities.OfType<Service>())
        {
            Add(service.ExecutablePath, service.Id);
        }

        foreach (var task in taskResult.Entities.OfType<ScheduledTask>())
        {
            Add(task.Action, task.Id);
        }

        foreach (var runtime in runtimeResult.Entities)
        {
            Add(runtime.Path, runtime.Id); // covers both Runtime and Sdk entities
        }

        return map;
    }

    private async Task<NativeBinaryRow> AnalyzeBinary(string path, List<string> owners, IReadOnlyDictionary<string, string> ldconfigCache, List<DiscoveryError> errors)
    {
        var infoResult = fileSystemReader.GetFileInfo(path);
        if (!infoResult.Success)
        {
            var status = infoResult.Status == OperationStatus.AccessDenied ? NativeBinaryFileStatus.AccessDenied : NativeBinaryFileStatus.NotFound;
            if (status == NativeBinaryFileStatus.AccessDenied)
            {
                errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{path}: access denied", IsPermissionFailure = true });
            }

            return new NativeBinaryRow { Path = path, FileStatus = status, OwnerEntityIds = owners };
        }

        if (infoResult.Value!.SizeBytes > MaxAnalysisSizeBytes)
        {
            return new NativeBinaryRow
            {
                Path = path,
                FileStatus = NativeBinaryFileStatus.SkippedTooLarge,
                SizeBytes = infoResult.Value.SizeBytes,
                LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
                OwnerEntityIds = owners
            };
        }

        var bytesResult = await fileSystemReader.ReadBytesAsync(path, CancellationToken.None);
        if (!bytesResult.Success)
        {
            var status = bytesResult.Status == OperationStatus.AccessDenied ? NativeBinaryFileStatus.AccessDenied : NativeBinaryFileStatus.NotFound;
            if (status == NativeBinaryFileStatus.AccessDenied)
            {
                errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{path}: access denied", IsPermissionFailure = true });
            }

            return new NativeBinaryRow
            {
                Path = path,
                FileStatus = status,
                SizeBytes = infoResult.Value.SizeBytes,
                LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
                OwnerEntityIds = owners
            };
        }

        var elfAnalysis = elfParser.Parse(bytesResult.Value!);

        return new NativeBinaryRow
        {
            Path = path,
            FileStatus = NativeBinaryFileStatus.Found,
            SizeBytes = infoResult.Value.SizeBytes,
            LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
            OwnerEntityIds = owners,
            ElfAnalysis = elfAnalysis
        };
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildKnownBinaryIndex(List<NativeBinaryRow> rows)
    {
        var byFileName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var row in rows.Where(r => r.FileStatus == NativeBinaryFileStatus.Found))
        {
            var lastSlash = row.Path.LastIndexOf('/');
            var fileName = lastSlash >= 0 ? row.Path[(lastSlash + 1)..] : row.Path;

            if (!byFileName.TryGetValue(fileName, out var paths))
            {
                paths = [];
                byFileName[fileName] = paths;
            }

            paths.Add(row.Path);
        }

        return byFileName.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
    }

    private NativeBinaryRow ResolveDependencies(NativeBinaryRow row, Dictionary<string, IReadOnlyList<string>> knownBinaryPathsByFileName, IReadOnlyDictionary<string, string> ldconfigCache)
    {
        if (row.ElfAnalysis is not { Dependencies.Count: > 0 } elf)
        {
            return row;
        }

        var resolved = elf.Dependencies
            .Select(name => libraryResolver.Resolve(name, row.Path, elf.RPath, elf.RunPath, knownBinaryPathsByFileName, ldconfigCache))
            .ToList();

        return row with { ResolvedDependencies = resolved };
    }

    /// <summary>Pure mapping, unit-testable against a synthetic NativeBinaryRow.</summary>
    internal static CoreDll BuildEntity(NativeBinaryRow row)
    {
        var entity = new CoreDll
        {
            Id = $"dll:{row.Path}",
            Name = LastPathSegment(row.Path),
            Type = "NativeBinary",
            Source = EvidenceSources.FileSystem,
            Status = row.FileStatus == NativeBinaryFileStatus.Found ? EntityStatus.Referenced : EntityStatus.Unknown,
            Confidence = row.FileStatus == NativeBinaryFileStatus.Found ? Confidence.VeryHigh() : Confidence.Medium(),
            Path = row.Path,
            Architecture = row.ElfAnalysis?.Architecture ?? EntityArchitecture.Unknown,
            ReferencedByEntityIds = row.OwnerEntityIds
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = row.Path, Detail = string.Join(",", row.OwnerEntityIds) });

        entity.SetMetadata("FileStatus", row.FileStatus.ToString());
        if (row.SizeBytes is not null) entity.SetMetadata("SizeBytes", row.SizeBytes.Value.ToString());
        if (row.LastWriteTimeUtc is not null) entity.SetMetadata("LastWriteTimeUtc", row.LastWriteTimeUtc.Value.ToString("O"));

        if (row.ElfAnalysis is { } elf)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ElfMetadata, Location = row.Path });

            entity.SetMetadata("ElfParseStatus", elf.Status.ToString());
            entity.SetMetadata("ElfClass", elf.Class.ToString());
            entity.SetMetadata("ElfEndian", elf.Endian.ToString());
            if (elf.Machine is not null) entity.SetMetadata("Machine", elf.Machine);
            if (elf.Diagnostic is not null) entity.SetMetadata("ElfDiagnostic", elf.Diagnostic);

            for (var i = 0; i < elf.RPath.Count; i++) entity.SetMetadata($"RPath{i}", elf.RPath[i]);
            for (var i = 0; i < elf.RunPath.Count; i++) entity.SetMetadata($"RunPath{i}", elf.RunPath[i]);

            for (var i = 0; i < row.ResolvedDependencies.Count; i++)
            {
                var dep = row.ResolvedDependencies[i];
                entity.SetMetadata($"Dependency{i}.Name", dep.LibraryName);
                entity.SetMetadata($"Dependency{i}.Status", dep.Status.ToString());
                if (dep.ResolvedPath is not null) entity.SetMetadata($"Dependency{i}.ResolvedPath", dep.ResolvedPath);
                if (dep.Source is not null) entity.SetMetadata($"Dependency{i}.Source", dep.Source);
                if (dep.Candidates.Count > 0) entity.SetMetadata($"Dependency{i}.Candidates", string.Join(",", dep.Candidates));

                entity.AddEvidence(new EvidenceRecord
                {
                    Type = EvidenceType.BinaryImport,
                    Location = "ELF dynamic section",
                    Detail = $"DT_NEEDED={dep.LibraryName}"
                });
            }
        }

        return entity;
    }

    private static string LastPathSegment(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }
}
