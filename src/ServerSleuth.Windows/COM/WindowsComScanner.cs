using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Registry;
using CoreComComponent = ServerSleuth.Core.Models.ComComponent;

namespace ServerSleuth.Windows.COM;

/// <summary>
/// Discovers registered COM/ActiveX components across all three CLSID registry locations.
/// This is registry discovery only — "Registered" is never conflated with "observed in use"
/// (see skill.md §9, §14); actual usage correlation is Phase 5's job. DLL deep analysis (PE
/// architecture parsing, dependency walking) is explicitly out of scope per skill.md §2/§18 —
/// only file existence/size/timestamp and the safe, non-executing FileVersionInfo API are used.
/// </summary>
public sealed class WindowsComScanner(
    IWindowsRegistryReader registryReader,
    IFileSystemReader fileSystemReader,
    IFileVersionMetadataReader fileVersionReader,
    ISecretRedactor secretRedactor,
    ILogger<WindowsComScanner> logger) : IDiscoveryScanner
{
    public string Id => "windows-com-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<CoreComComponent>();
        var partialFailures = new List<string>();
        var deniedSources = 0;

        foreach (var source in ComRegistrationSource.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clsidNamesResult = registryReader.GetSubKeyNames(source.Hive, source.View, source.Path);
            if (!clsidNamesResult.Success)
            {
                deniedSources++;
                partialFailures.Add($"{source.Label}: {clsidNamesResult.Status}");
                logger.LogWarning(ScannerLogEvents.PermissionDenied, "{ScannerId} could not enumerate {Source}: {Status}", Id, source.Label, clsidNamesResult.Status);
                continue;
            }

            foreach (var clsid in clsidNamesResult.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var readResult = ComClsidReader.Read(registryReader, source.Hive, source.View, source.Path, clsid);
                if (!readResult.Success)
                {
                    partialFailures.Add($"{source.Label}: {readResult.FailureReason}");
                    continue;
                }

                entities.Add(BuildEntity(readResult.Row!, source, secretRedactor, fileSystemReader, fileVersionReader));
            }
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} components", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var errors = partialFailures.Select(failure => new DiscoveryError { ScannerId = Id, Message = failure, IsPermissionFailure = true }).ToList();

        var status = deniedSources switch
        {
            var n when n == ComRegistrationSource.All.Count => ScannerStatus.AccessDenied,
            var n when n > 0 || errors.Count > 0 => ScannerStatus.PartiallySupported,
            _ => ScannerStatus.Supported
        };

        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors });
    }

    /// <summary>Pure mapping aside from the optional file-metadata lookups, unit-testable
    /// against a synthetic ComClsidRow with no registry/filesystem access (pass nulls for
    /// fileSystemReader/fileVersionReader to skip those checks entirely). secretRedactor is
    /// required (not optional) — every raw registry value this scanner captures must be
    /// redacted before it becomes metadata, never skippable. See skill.md §24.</summary>
    internal static CoreComComponent BuildEntity(
        ComClsidRow row,
        ComRegistrationSource source,
        ISecretRedactor secretRedactor,
        IFileSystemReader? fileSystemReader = null,
        IFileVersionMetadataReader? fileVersionReader = null)
    {
        var canonicalClsid = row.Clsid.ToUpperInvariant();
        var name = row.ProgId ?? row.Name ?? canonicalClsid;
        var serverType = (row.InprocServer32, row.LocalServer32) switch
        {
            (not null, not null) => "Both",
            (not null, null) => "InProcess",
            (null, not null) => "LocalServer",
            (null, null) => "Unknown"
        };

        var primaryServerPath = ResolvedPath(row.InprocServer32) ?? ResolvedPath(row.LocalServer32);

        var entity = new CoreComComponent
        {
            Id = $"com:{source.RegistrationScope}:{source.RegistryViewLabel}:{canonicalClsid}",
            Name = name,
            Type = "ComComponent",
            Source = EvidenceSources.WindowsRegistry,
            Status = EntityStatus.Installed, // Registered — never conflated with Used, see class remarks.
            Confidence = Confidence.VeryHigh(),
            Path = primaryServerPath,
            Version = row.VersionValue,
            Clsid = canonicalClsid,
            ProgId = row.ProgId,
            InprocServer32 = ResolvedPath(row.InprocServer32),
            LocalServer32 = ResolvedPath(row.LocalServer32),
            ThreadingModel = row.ThreadingModel,
            TypeLibrary = row.TypeLibClsid
        };

        entity.AddEvidence(new EvidenceRecord
        {
            Type = EvidenceType.Registry,
            Location = $@"{source.Label}\{canonicalClsid}",
            Detail = $"Scope={source.RegistrationScope}; View={source.RegistryViewLabel}"
        });

        entity.SetMetadata("RegistrationStatus", "Registered");
        entity.SetMetadata("RegistrationScope", source.RegistrationScope);
        entity.SetMetadata("RegistryView", source.RegistryViewLabel);
        entity.SetMetadata("ServerType", serverType);

        if (row.InprocServer32 is { RawReferenceDetected: true } inprocRaw)
        {
            entity.SetMetadata("InprocServer32Status", "RawReferenceDetected");
            SetRedactedMetadata(entity, secretRedactor, "InprocServer32RawValue", inprocRaw.RawValue);
        }

        if (row.LocalServer32 is { RawReferenceDetected: true } localRaw)
        {
            entity.SetMetadata("LocalServer32Status", "RawReferenceDetected");
            SetRedactedMetadata(entity, secretRedactor, "LocalServer32RawValue", localRaw.RawValue);
        }

        if (row.LocalServer32?.Arguments is { } arguments)
        {
            SetRedactedMetadata(entity, secretRedactor, "LocalServer32Arguments", arguments);
        }

        ApplyFileVerification(entity, primaryServerPath, fileSystemReader, fileVersionReader);

        return entity;
    }

    private static void SetRedactedMetadata(CoreComComponent entity, ISecretRedactor secretRedactor, string key, string rawValue)
    {
        if (secretRedactor.ContainsSecret(rawValue))
        {
            entity.SetMetadata("SecretDetected", "true");
        }

        entity.SetMetadata(key, secretRedactor.Redact(rawValue));
    }

    private static string? ResolvedPath(ServerReference? reference) =>
        reference is { RawReferenceDetected: false } ? reference.ExecutablePath : null;

    private static void ApplyFileVerification(
        CoreComComponent entity, string? serverPath, IFileSystemReader? fileSystemReader, IFileVersionMetadataReader? fileVersionReader)
    {
        if (serverPath is null || fileSystemReader is null)
        {
            return;
        }

        var infoResult = fileSystemReader.GetFileInfo(serverPath);
        if (!infoResult.Success)
        {
            entity.SetMetadata("ServerPathStatus", infoResult.Status switch
            {
                OperationStatus.AccessDenied => "AccessDenied",
                OperationStatus.NotFound => "NotFound",
                _ => "Unavailable"
            });
            return;
        }

        entity.SetMetadata("ServerFileSizeBytes", infoResult.Value!.SizeBytes.ToString());
        entity.SetMetadata("ServerLastWriteTimeUtc", infoResult.Value.LastWriteTimeUtc.ToString("O"));

        var versionInfo = fileVersionReader?.TryRead(serverPath);
        if (versionInfo is null)
        {
            return;
        }

        entity.Version ??= versionInfo.FileVersion ?? versionInfo.ProductVersion;
        entity.Publisher = versionInfo.CompanyName;

        if (versionInfo.ProductName is not null)
        {
            entity.SetMetadata("ServerProductName", versionInfo.ProductName);
        }
    }
}
