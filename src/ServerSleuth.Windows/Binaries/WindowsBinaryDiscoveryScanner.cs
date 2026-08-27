using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.COM;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Configuration;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using CoreDll = ServerSleuth.Core.Models.Dll;

namespace ServerSleuth.Windows.Binaries;

/// <summary>
/// Discovers native/managed binaries under bounded roots derived from already-discovered
/// entities (IIS/Service/ScheduledTask/COM) — never a filesystem-wide scan. Reuses
/// IisScanner/WindowsServiceScanner/WindowsScheduledTaskScanner/WindowsComScanner directly
/// (re-running them, not duplicating them) purely to obtain paths. See skill.md §2-4.
/// </summary>
public sealed class WindowsBinaryDiscoveryScanner(
    IisScanner iisScanner,
    WindowsServiceScanner serviceScanner,
    WindowsScheduledTaskScanner scheduledTaskScanner,
    WindowsComScanner comScanner,
    IFileSystemReader fileSystemReader,
    IFileVersionMetadataReader fileVersionReader,
    IPeAnalyzer peAnalyzer,
    ISecretRedactor secretRedactor,
    ILogger<WindowsBinaryDiscoveryScanner> logger) : IDiscoveryScanner
{
    public string Id => "windows-binary-discovery-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var iisResult = await iisScanner.ScanAsync(context, cancellationToken);
        var serviceResult = await serviceScanner.ScanAsync(context, cancellationToken);
        var taskResult = await scheduledTaskScanner.ScanAsync(context, cancellationToken);
        var comResult = await comScanner.ScanAsync(context, cancellationToken);

        var comComponents = comResult.Entities.OfType<ComComponent>().ToList();

        var scanRoots = MergeRoots(
            ServerSleuth.Windows.Configuration.ScanRootCollector.Collect(
                iisResult.Entities.OfType<WebSite>().ToList(),
                iisResult.Entities.OfType<Application>().ToList(),
                serviceResult.Entities.OfType<Service>().ToList(),
                taskResult.Entities.OfType<ScheduledTask>().ToList()),
            ComScanRootCollector.Collect(comComponents));

        var pathToRoots = new Dictionary<string, List<ScanRoot>>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<DiscoveryError>();
        var limitReached = false;

        foreach (var root in scanRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SystemDirectoryExclusion.IsSystemOwned(root.Path))
            {
                // Never walk %windir% and everything under it (System32, SysWOW64, WinSxS,
                // ...) — thousands of OS-shipped files with no migration relevance. The
                // specific COM-referenced file (if that's how this root arose) is still
                // checked directly below, regardless of this skip. See skill.md §16.
                continue;
            }

            var walk = BoundedDirectoryWalker.Walk(fileSystemReader, root.Path, BinaryDiscoveryDefaults.SearchPatterns);

            if (walk.DepthLimitReached)
            {
                errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{root.Path}: DepthLimitReached" });
                limitReached = true;
            }

            if (walk.FileLimitReached)
            {
                errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{root.Path}: FileLimitReached" });
                limitReached = true;
            }

            foreach (var deniedDir in walk.AccessDeniedDirectories)
            {
                errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{deniedDir}: AccessDenied", IsPermissionFailure = true });
            }

            foreach (var file in walk.Files)
            {
                AddContributingRoot(pathToRoots, file, root);
            }
        }

        // COM-referenced files are checked directly even if their directory was never walked
        // (or the file simply doesn't exist) — a dangling COM registration is valuable
        // migration evidence, not something to silently drop. See skill.md §16.
        foreach (var component in comComponents)
        {
            AddDirectComReference(pathToRoots, component.InprocServer32, component.Id, "COM InprocServer32 path");
            AddDirectComReference(pathToRoots, component.LocalServer32, component.Id, "COM LocalServer32 path");
        }

        var entities = pathToRoots
            .Select(kvp => BuildEntity(ReadRow(kvp.Key, kvp.Value), secretRedactor))
            .ToList();

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} binaries", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = errors.Count > 0 || limitReached ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    private static void AddContributingRoot(Dictionary<string, List<ScanRoot>> map, string path, ScanRoot root)
    {
        if (!map.TryGetValue(path, out var roots))
        {
            roots = [];
            map[path] = roots;
        }

        roots.Add(root);
    }

    private static void AddDirectComReference(Dictionary<string, List<ScanRoot>> map, string? serverPath, string ownerId, string reason)
    {
        if (serverPath is null)
        {
            return;
        }

        var root = new ScanRoot
        {
            Path = System.IO.Path.GetDirectoryName(serverPath) ?? serverPath,
            Source = "COM",
            OwnerEntityId = ownerId,
            Reason = reason,
            Confidence = Confidence.VeryHigh()
        };

        AddContributingRoot(map, serverPath, root);
    }

    private static Confidence DetermineConfidence(IReadOnlyList<ScanRoot> contributingRoots) =>
        contributingRoots.Count > 0 ? new Confidence(contributingRoots.Max(r => r.Confidence.Value)) : Confidence.Medium();

    private static List<ScanRoot> MergeRoots(IReadOnlyList<ScanRoot> a, IReadOnlyList<ScanRoot> b) =>
        a.Concat(b)
            .GroupBy(r => r.Path.TrimEnd('\\').ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

    private BinaryDiscoveryRow ReadRow(string path, IReadOnlyList<ScanRoot> contributingRoots)
    {
        var fileName = System.IO.Path.GetFileName(path);
        var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

        var infoResult = fileSystemReader.GetFileInfo(path);
        if (!infoResult.Success)
        {
            return new BinaryDiscoveryRow
            {
                Path = path,
                FileName = fileName,
                Extension = extension,
                FileStatus = infoResult.Status == OperationStatus.AccessDenied ? BinaryFileStatus.AccessDenied : BinaryFileStatus.NotFound,
                ContributingRoots = contributingRoots
            };
        }

        var peAnalysis = peAnalyzer.Analyze(path);
        var versionMetadata = fileVersionReader.TryRead(path);

        return new BinaryDiscoveryRow
        {
            Path = path,
            FileName = fileName,
            Extension = extension,
            FileStatus = BinaryFileStatus.Found,
            SizeBytes = infoResult.Value!.SizeBytes,
            LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
            ContributingRoots = contributingRoots,
            PeAnalysis = peAnalysis,
            VersionMetadata = versionMetadata
        };
    }

    /// <summary>Pure mapping aside from the PE/version reads already done in ReadRow, unit-
    /// testable against a synthetic BinaryDiscoveryRow.</summary>
    internal static CoreDll BuildEntity(BinaryDiscoveryRow row, ISecretRedactor secretRedactor)
    {
        var ownerIds = row.ContributingRoots
            .Select(r => r.OwnerEntityId)
            .Where(id => id is not null)
            .Distinct()
            .ToList()!;

        var entity = new CoreDll
        {
            Id = $"dll:{row.Path}",
            Name = row.FileName,
            Type = row.PeAnalysis?.BinaryType?.ToString() ?? "UnknownBinary",
            Source = EvidenceSources.FileSystem,
            Status = row.FileStatus == BinaryFileStatus.Found ? EntityStatus.Referenced : EntityStatus.Unknown,
            Confidence = DetermineConfidence(row.ContributingRoots),
            Path = row.Path,
            Version = row.VersionMetadata?.FileVersion ?? row.VersionMetadata?.ProductVersion,
            Publisher = row.VersionMetadata?.CompanyName,
            Architecture = row.PeAnalysis?.Architecture ?? EntityArchitecture.Unknown,
            ReferencedByEntityIds = ownerIds!
        };

        foreach (var root in row.ContributingRoots)
        {
            entity.AddEvidence(new EvidenceRecord
            {
                Type = EvidenceType.FileSystem,
                Location = row.Path,
                Detail = $"{root.Source}: {root.Reason}"
            });
        }

        entity.SetMetadata("FileStatus", row.FileStatus.ToString());
        if (row.SizeBytes is not null) entity.SetMetadata("SizeBytes", row.SizeBytes.Value.ToString());
        if (row.LastWriteTimeUtc is not null) entity.SetMetadata("LastWriteTimeUtc", row.LastWriteTimeUtc.Value.ToString("O"));
        if (row.VersionMetadata?.ProductName is not null) entity.SetMetadata("ProductName", row.VersionMetadata.ProductName);

        if (row.PeAnalysis is { } pe)
        {
            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.PeMetadata, Location = row.Path });

            entity.SetMetadata("PeParseStatus", pe.Status.ToString());
            if (pe.BinaryType is not null) entity.SetMetadata("BinaryType", pe.BinaryType.Value.ToString());
            entity.SetMetadata("IsManaged", pe.IsManaged.ToString());
            if (pe.Machine is not null) entity.SetMetadata("Machine", pe.Machine);
            entity.SetMetadata("Is64BitImage", pe.Is64BitImage.ToString());
            if (pe.Subsystem is not null) entity.SetMetadata("Subsystem", pe.Subsystem);
            if (pe.ImageSizeBytes is not null) entity.SetMetadata("ImageSizeBytes", pe.ImageSizeBytes.Value.ToString());
            if (pe.TimestampUtc is not null) entity.SetMetadata("PeTimestampUtc", pe.TimestampUtc.Value.ToString("O"));

            if (pe.Imports.Count > 0)
            {
                entity.SetMetadata("Imports", secretRedactor.Redact(string.Join(",", pe.Imports)));
            }

            entity.SetMetadata("DelayImportAnalysis", pe.DelayImportsSupported ? "Supported" : "Unsupported");
        }

        for (var i = 0; i < row.ContributingRoots.Count; i++)
        {
            var root = row.ContributingRoots[i];
            entity.SetMetadata($"Root{i}.Source", root.Source);
            entity.SetMetadata($"Root{i}.Reason", root.Reason);
            if (root.OwnerEntityId is not null) entity.SetMetadata($"Root{i}.OwnerEntityId", root.OwnerEntityId);
        }

        return entity;
    }
}
