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
using ServerSleuth.Linux.Common;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Systemd;
using CoreConfiguration = ServerSleuth.Core.Models.Configuration;

namespace ServerSleuth.Linux.Configuration;

/// <summary>
/// Discovers Linux configuration files under bounded scan roots derived from well-known
/// technology directories and already-discovered Service/ScheduledTask entities — never an
/// arbitrary or filesystem-wide scan. Reuses LinuxSystemdServiceScanner and
/// LinuxScheduledTaskScanner directly (re-running them, not duplicating their logic) purely to
/// obtain executable paths that become application scan roots, exactly as
/// WindowsConfigurationScanner re-runs IIS/Service/ScheduledTask discovery. See skill.md
/// (Phase 6E) §2-6.
/// </summary>
public sealed class LinuxConfigurationScanner(
    LinuxSystemdServiceScanner systemdScanner,
    LinuxScheduledTaskScanner scheduledTaskScanner,
    IFileSystemReader fileSystemReader,
    ISecretRedactor secretRedactor,
    ILogger<LinuxConfigurationScanner> logger) : IDiscoveryScanner
{
    private const long MaxInspectionSizeBytes = 1024 * 1024; // 1 MB — mirrors Windows's cap, see skill.md §24 (Windows) / performance boundary (Phase 6E §30)

    private static readonly IReadOnlyList<string> SearchPatterns =
    [
        "*.json", "*.xml", "*.ini", "*.yaml", "*.yml", "*.properties", "*.env", "*.conf", "*.cnf",
        "*.service", "*.socket", "*.timer", "*.mount", "*.target", "*.path", "*.slice",
        "sshd_config"
    ];

    public string Id => "linux-configuration-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var systemdResult = await systemdScanner.ScanAsync(context, cancellationToken);
        var taskResult = await scheduledTaskScanner.ScanAsync(context, cancellationToken);

        var services = systemdResult.Entities.OfType<Service>().ToList();
        var scheduledTasks = taskResult.Entities.OfType<ScheduledTask>().ToList();
        var scanRoots = LinuxScanRootCollector.Collect(services, scheduledTasks);

        var seenPaths = new HashSet<string>(StringComparer.Ordinal); // Linux paths are case-sensitive — never StringComparer.OrdinalIgnoreCase here
        var entities = new List<CoreConfiguration>();
        var errors = new List<DiscoveryError>();
        var additionalExplicitFiles = new List<ScanRoot>();

        foreach (var root in scanRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var filePath in EnumerateRoot(root, errors))
            {
                if (!seenPaths.Add(filePath))
                {
                    continue; // already found via another pattern/root
                }

                var row = ReadRow(filePath, root);
                entities.Add(BuildEntity(row, secretRedactor));

                if (row.TechnologyFacts.Any(kv => kv.Key.StartsWith("EnvironmentFile", StringComparison.Ordinal)))
                {
                    foreach (var (key, value) in row.TechnologyFacts.Where(kv => kv.Key.StartsWith("EnvironmentFile", StringComparison.Ordinal)))
                    {
                        if (value.StartsWith('/'))
                        {
                            additionalExplicitFiles.Add(new ScanRoot
                            {
                                Path = value,
                                Source = "Systemd",
                                OwnerEntityId = row.OwnerEntityId,
                                Reason = "systemd EnvironmentFile reference",
                                Confidence = Confidence.High(),
                                IsExplicitFile = true
                            });
                        }
                    }
                }
            }
        }

        // Second pass: explicit EnvironmentFile references discovered above — never expanded
        // further (an .env file is never itself scanned for more EnvironmentFile= directives).
        foreach (var explicitRoot in additionalExplicitFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seenPaths.Add(explicitRoot.Path))
            {
                continue;
            }

            var row = ReadRow(explicitRoot.Path, explicitRoot);
            entities.Add(BuildEntity(row, secretRedactor));
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} configuration files", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    private IEnumerable<string> EnumerateRoot(ScanRoot root, List<DiscoveryError> errors)
    {
        if (root.IsExplicitFile)
        {
            if (fileSystemReader.Exists(root.Path))
            {
                yield return root.Path;
            }

            yield break;
        }

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
                yield return filePath;
            }
        }
    }

    private ConfigurationFileRow ReadRow(string filePath, ScanRoot root)
    {
        var lastSlash = filePath.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? filePath[(lastSlash + 1)..] : filePath;
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
                ScanRoot = root,
                IsSymlink = infoResult.Value.IsReparsePoint
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
                ScanRoot = root,
                IsSymlink = infoResult.Value.IsReparsePoint
            };
        }

        var (parseStatus, sections) = ValidateStructure(format, textResult.Value!);
        var analysis = ConfigurationContentAnalyzer.Analyze(textResult.Value!, secretRedactor);
        var technologyFacts = LinuxConfigurationTechnologyAnalyzer.Analyze(root.Source, textResult.Value!);

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
            DetectedSections = sections,
            IsSymlink = infoResult.Value.IsReparsePoint,
            TechnologyFacts = technologyFacts
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
            case ConfigurationFormat.EnvFile:
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
        if (row.IsSymlink) entity.SetMetadata("IsSymlink", "True");

        foreach (var (key, value) in row.TechnologyFacts)
        {
            // Technology facts may themselves contain secret-shaped values (e.g. a systemd
            // ExecStart embedding a --token=... argument) — redact before storage, same rule as
            // the generic analysis path.
            entity.SetMetadata($"{row.ScanRoot.Source}.{key}", secretRedactor.Redact(value));
        }

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

            for (var i = 0; i < analysis.NetworkStorageReferences.Count; i++)
            {
                var n = analysis.NetworkStorageReferences[i];
                entity.SetMetadata($"NetworkStorage{i}.Protocol", n.Protocol);
                entity.SetMetadata($"NetworkStorage{i}.Server", n.Server);
                entity.SetMetadata($"NetworkStorage{i}.Path", n.Path);
            }

            for (var i = 0; i < analysis.UnixSocketReferences.Count; i++)
            {
                entity.SetMetadata($"UnixSocket{i}", analysis.UnixSocketReferences[i]);
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
        references.AddRange(analysis.NetworkStorageReferences.Select(n => $"NetworkStorage: {n.Protocol}://{n.Server}{n.Path}"));
        references.AddRange(analysis.UnixSocketReferences.Select(s => $"UnixSocket: {s}"));
        references.AddRange(analysis.EnvironmentVariableReferences.Select(v => $"EnvVar: {v}"));
        references.AddRange(analysis.RuntimeReferences.Select(r => $"Runtime: {r}"));
        references.AddRange(analysis.RuntimeVersionReferences.Select(v => $"RuntimeVersion: {v}"));
        return references;
    }
}
