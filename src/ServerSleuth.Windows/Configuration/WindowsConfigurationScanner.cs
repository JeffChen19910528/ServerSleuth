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
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using CoreConfiguration = ServerSleuth.Core.Models.Configuration;

namespace ServerSleuth.Windows.Configuration;

/// <summary>
/// Discovers configuration files under scan roots derived from IIS/Service/ScheduledTask
/// discovery — never an arbitrary or filesystem-wide scan. Reuses IisScanner,
/// WindowsServiceScanner, and WindowsScheduledTaskScanner directly (re-running them, not
/// duplicating their logic) purely to obtain the physical/executable paths that become scan
/// roots. See skill.md §2-5.
/// </summary>
public sealed class WindowsConfigurationScanner(
    IisScanner iisScanner,
    WindowsServiceScanner serviceScanner,
    WindowsScheduledTaskScanner scheduledTaskScanner,
    IFileSystemReader fileSystemReader,
    ISecretRedactor secretRedactor,
    ILogger<WindowsConfigurationScanner> logger) : IDiscoveryScanner
{
    private const long MaxInspectionSizeBytes = 1024 * 1024; // 1 MB — see skill.md §24.

    private static readonly IReadOnlyList<string> SearchPatterns =
        ["*.config", "*.json", "*.xml", "*.ini", "*.yaml", "*.yml", "*.properties"];

    public string Id => "windows-configuration-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var iisResult = await iisScanner.ScanAsync(context, cancellationToken);
        var serviceResult = await serviceScanner.ScanAsync(context, cancellationToken);
        var taskResult = await scheduledTaskScanner.ScanAsync(context, cancellationToken);

        var scanRoots = ScanRootCollector.Collect(
            iisResult.Entities.OfType<WebSite>().ToList(),
            iisResult.Entities.OfType<Application>().ToList(),
            serviceResult.Entities.OfType<Service>().ToList(),
            taskResult.Entities.OfType<ScheduledTask>().ToList());

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<CoreConfiguration>();
        var errors = new List<DiscoveryError>();

        foreach (var root in scanRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var pattern in SearchPatterns)
            {
                var filesResult = fileSystemReader.EnumerateFiles(root.Path, pattern, recursive: true);
                if (!filesResult.Success)
                {
                    if (filesResult.Status != OperationStatus.NotFound) // a root simply not existing isn't an error worth reporting
                    {
                        errors.Add(new DiscoveryError { ScannerId = Id, Message = $"{root.Path} ({pattern}): {filesResult.Status}", IsPermissionFailure = filesResult.Status == OperationStatus.AccessDenied });
                    }
                    continue;
                }

                foreach (var filePath in filesResult.Value!)
                {
                    if (!seenPaths.Add(filePath)) // already found via another pattern/root
                    {
                        continue;
                    }

                    entities.Add(BuildEntity(ReadRow(filePath, root), secretRedactor));
                }
            }
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} configuration files", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    private ConfigurationFileRow ReadRow(string filePath, ScanRoot root)
    {
        var fileName = Path.GetFileName(filePath);
        var format = ConfigurationFormatDetector.FromFileName(fileName);

        var infoResult = fileSystemReader.GetFileInfo(filePath);
        if (!infoResult.Success)
        {
            return new ConfigurationFileRow
            {
                Path = filePath,
                FileName = fileName,
                Format = format,
                ParseStatus = infoResult.Status == OperationStatus.AccessDenied ? ConfigurationParseStatus.AccessDenied : ConfigurationParseStatus.NotFound,
                ScanRoot = root
            };
        }

        if (infoResult.Value!.SizeBytes > MaxInspectionSizeBytes)
        {
            return new ConfigurationFileRow
            {
                Path = filePath,
                FileName = fileName,
                Format = format,
                ParseStatus = ConfigurationParseStatus.SkippedTooLarge,
                SizeBytes = infoResult.Value.SizeBytes,
                LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
                ScanRoot = root
            };
        }

        var textResult = fileSystemReader.ReadTextAsync(filePath, CancellationToken.None).GetAwaiter().GetResult();
        if (!textResult.Success)
        {
            return new ConfigurationFileRow
            {
                Path = filePath,
                FileName = fileName,
                Format = format,
                ParseStatus = textResult.Status switch
                {
                    OperationStatus.AccessDenied => ConfigurationParseStatus.AccessDenied,
                    OperationStatus.NotFound => ConfigurationParseStatus.NotFound,
                    _ => ConfigurationParseStatus.Unreadable
                },
                SizeBytes = infoResult.Value.SizeBytes,
                LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
                ScanRoot = root
            };
        }

        var (parseStatus, sections) = ValidateStructure(format, textResult.Value!);
        var analysis = ConfigurationContentAnalyzer.Analyze(textResult.Value!, secretRedactor);

        return new ConfigurationFileRow
        {
            Path = filePath,
            FileName = fileName,
            Format = format,
            ParseStatus = parseStatus,
            SizeBytes = infoResult.Value.SizeBytes,
            LastWriteTimeUtc = infoResult.Value.LastWriteTimeUtc,
            OwnerEntityId = root.OwnerEntityId,
            ScanRoot = root,
            Analysis = analysis,
            DetectedSections = sections
        };
    }

    private static (ConfigurationParseStatus Status, IReadOnlyList<string> Sections) ValidateStructure(ConfigurationFormat format, string text)
    {
        switch (format)
        {
            case ConfigurationFormat.Json:
                var (jsonValid, jsonSections) = StructuralValidator.TryValidateJson(text);
                return (jsonValid ? ConfigurationParseStatus.Parsed : ConfigurationParseStatus.PartiallyParsed, jsonSections);
            case ConfigurationFormat.Xml:
                var (xmlValid, xmlSections) = StructuralValidator.TryValidateXml(text);
                return (xmlValid ? ConfigurationParseStatus.Parsed : ConfigurationParseStatus.PartiallyParsed, xmlSections);
            case ConfigurationFormat.Ini:
            case ConfigurationFormat.Properties:
            case ConfigurationFormat.Yaml:
                return (ConfigurationParseStatus.Parsed, []);
            default:
                return (ConfigurationParseStatus.Unsupported, []);
        }
    }

    /// <summary>Pure mapping, unit-testable against a synthetic ConfigurationFileRow.</summary>
    internal static CoreConfiguration BuildEntity(ConfigurationFileRow row, ISecretRedactor secretRedactor)
    {
        var entity = new CoreConfiguration
        {
            Id = $"configuration:{row.Path}",
            Name = row.FileName,
            Type = "Configuration",
            Source = EvidenceSources.FileSystem,
            Status = EntityStatus.Configured,
            Confidence = row.ParseStatus == ConfigurationParseStatus.Parsed ? row.ScanRoot.Confidence : Confidence.Medium(),
            Path = row.Path,
            Format = row.Format.ToString(),
            DetectedSections = row.DetectedSections,
            DetectedDependencyReferences = BuildDependencyReferences(row.Analysis),
            SecretDetected = row.Analysis?.SecretDetected ?? false
        };

        entity.AddEvidence(new EvidenceRecord
        {
            Type = EvidenceType.ConfigurationFile,
            Location = row.Path,
            Detail = $"ScanRoot={row.ScanRoot.Path}; Source={row.ScanRoot.Source}"
        });

        entity.SetMetadata("ParseStatus", row.ParseStatus.ToString());
        entity.SetMetadata("ScanRootPath", row.ScanRoot.Path);
        entity.SetMetadata("ScanRootSource", row.ScanRoot.Source);
        if (row.OwnerEntityId is not null) entity.SetMetadata("OwnerEntityId", row.OwnerEntityId);
        if (row.SizeBytes is not null) entity.SetMetadata("SizeBytes", row.SizeBytes.Value.ToString());
        if (row.LastWriteTimeUtc is not null) entity.SetMetadata("LastWriteTimeUtc", row.LastWriteTimeUtc.Value.ToString("O"));

        if (row.Analysis is { } analysis)
        {
            for (var i = 0; i < analysis.ExternalEndpoints.Count; i++)
            {
                var e = analysis.ExternalEndpoints[i];
                entity.SetMetadata($"Endpoint{i}.Scheme", e.Scheme);
                entity.SetMetadata($"Endpoint{i}.Host", e.Host);
                if (e.Port is not null) entity.SetMetadata($"Endpoint{i}.Port", e.Port.Value.ToString());
                if (e.Path is not null) entity.SetMetadata($"Endpoint{i}.Path", e.Path);
            }

            for (var i = 0; i < analysis.DatabaseReferences.Count; i++)
            {
                var d = analysis.DatabaseReferences[i];
                entity.SetMetadata($"Database{i}.Type", d.Type);
                if (d.Host is not null) entity.SetMetadata($"Database{i}.Host", d.Host);
                if (d.Port is not null) entity.SetMetadata($"Database{i}.Port", d.Port.Value.ToString());
                if (d.Database is not null) entity.SetMetadata($"Database{i}.Name", d.Database);
            }

            for (var i = 0; i < analysis.NetworkPaths.Count; i++)
            {
                var u = analysis.NetworkPaths[i];
                entity.SetMetadata($"NetworkPath{i}.Server", u.Server);
                entity.SetMetadata($"NetworkPath{i}.Share", u.Share);
                if (u.Path is not null) entity.SetMetadata($"NetworkPath{i}.Path", u.Path);
            }
        }

        return entity;
    }

    private static IReadOnlyList<string> BuildDependencyReferences(ConfigurationAnalysisResult? analysis)
    {
        if (analysis is null)
        {
            return [];
        }

        var references = new List<string>();
        references.AddRange(analysis.ExternalEndpoints.Select(e => $"Endpoint: {e.Scheme}://{e.Host}{(e.Port is not null ? $":{e.Port}" : string.Empty)}"));
        references.AddRange(analysis.DatabaseReferences.Select(d => $"Database: {d.Type}{(d.Host is not null ? $"@{d.Host}" : string.Empty)}"));
        references.AddRange(analysis.NetworkPaths.Select(u => $"FileShare: \\\\{u.Server}\\{u.Share}"));
        references.AddRange(analysis.EnvironmentVariableReferences.Select(v => $"EnvVar: {v}"));
        references.AddRange(analysis.RuntimeReferences.Select(r => $"Runtime: {r}"));
        references.AddRange(analysis.RuntimeVersionReferences.Select(v => $"RuntimeVersion: {v}"));
        return references;
    }
}
