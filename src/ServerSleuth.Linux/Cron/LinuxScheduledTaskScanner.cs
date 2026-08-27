using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Common;
using CoreScheduledTask = ServerSleuth.Core.Models.ScheduledTask;

namespace ServerSleuth.Linux.Cron;

/// <summary>
/// Discovers cron-based scheduled jobs from a fixed, bounded set of well-known locations —
/// never a recursive walk of unrelated directories. Read-only: never runs a cron command,
/// never evaluates shell syntax. See skill.md (Phase 6B) §16-21. Reuses `Core.Models.
/// ScheduledTask` (Phase 1) directly — cron semantics (schedule expression, run-as user,
/// command, source file) map onto it without any Windows-only assumption.
/// </summary>
public sealed class LinuxScheduledTaskScanner(IFileSystemReader fileSystemReader, ISecretRedactor secretRedactor, ILogger<LinuxScheduledTaskScanner> logger)
    : IDiscoveryScanner
{
    private static readonly (string Directory, string Trigger)[] RunPartsDirectories =
    [
        ("/etc/cron.hourly", "Hourly"),
        ("/etc/cron.daily", "Daily"),
        ("/etc/cron.weekly", "Weekly"),
        ("/etc/cron.monthly", "Monthly")
    ];

    public string Id => "linux-scheduled-task-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<CoreScheduledTask>();
        var errors = new List<DiscoveryError>();

        ReadSystemCrontabFile("/etc/crontab", entities, errors, cancellationToken);
        ReadSystemCrontabDirectory("/etc/cron.d", entities, errors, cancellationToken);

        foreach (var (directory, trigger) in RunPartsDirectories)
        {
            ReadRunPartsDirectory(directory, trigger, entities, errors, cancellationToken);
        }

        ReadUserCrontabDirectory(entities, errors, cancellationToken);

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} scheduled jobs", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors });
    }

    private void ReadSystemCrontabFile(string path, List<CoreScheduledTask> entities, List<DiscoveryError> errors, CancellationToken cancellationToken)
    {
        var result = fileSystemReader.ReadTextAsync(path, cancellationToken).GetAwaiter().GetResult();
        if (!result.Success)
        {
            RecordSourceError(path, result.Status, errors);
            return;
        }

        var index = 0;
        foreach (var line in result.Value!.Split('\n'))
        {
            var entry = CronLineParser.ParseSystemCrontabLine(line);
            if (entry is not null)
            {
                entities.Add(BuildEntity(path, index++, entry, secretRedactor));
            }
        }
    }

    private void ReadSystemCrontabDirectory(string directory, List<CoreScheduledTask> entities, List<DiscoveryError> errors, CancellationToken cancellationToken)
    {
        var filesResult = fileSystemReader.EnumerateFiles(directory, "*");
        if (!filesResult.Success)
        {
            RecordSourceError(directory, filesResult.Status, errors);
            return;
        }

        foreach (var file in filesResult.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadSystemCrontabFile(file, entities, errors, cancellationToken);
        }
    }

    private void ReadRunPartsDirectory(string directory, string trigger, List<CoreScheduledTask> entities, List<DiscoveryError> errors, CancellationToken cancellationToken)
    {
        var filesResult = fileSystemReader.EnumerateFiles(directory, "*");
        if (!filesResult.Success)
        {
            RecordSourceError(directory, filesResult.Status, errors);
            return;
        }

        foreach (var file in filesResult.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entities.Add(BuildRunPartsEntity(directory, file, trigger));
        }
    }

    private void ReadUserCrontabDirectory(List<CoreScheduledTask> entities, List<DiscoveryError> errors, CancellationToken cancellationToken)
    {
        // Debian-style layout first; RHEL-style flat /var/spool/cron is the fallback.
        var debianResult = fileSystemReader.EnumerateFiles("/var/spool/cron/crontabs", "*");
        var sourceDirectory = "/var/spool/cron/crontabs";
        var filesResult = debianResult;

        if (!debianResult.Success && debianResult.Status == OperationStatus.NotFound)
        {
            sourceDirectory = "/var/spool/cron";
            filesResult = fileSystemReader.EnumerateFiles(sourceDirectory, "*");
        }

        if (!filesResult.Success)
        {
            RecordSourceError(sourceDirectory, filesResult.Status, errors);
            return;
        }

        foreach (var file in filesResult.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = fileSystemReader.ReadTextAsync(file, cancellationToken).GetAwaiter().GetResult();
            if (!result.Success)
            {
                RecordSourceError(file, result.Status, errors);
                continue;
            }

            var user = System.IO.Path.GetFileName(file);
            var index = 0;
            foreach (var line in result.Value!.Split('\n'))
            {
                var entry = CronLineParser.ParseUserCrontabLine(line);
                if (entry is not null)
                {
                    entities.Add(BuildEntity(file, index++, entry with { User = user }, secretRedactor));
                }
            }
        }
    }

    private static void RecordSourceError(string source, OperationStatus status, List<DiscoveryError> errors)
    {
        if (status == OperationStatus.NotFound)
        {
            return; // a source simply not present on this system is normal, not an error
        }

        errors.Add(new DiscoveryError
        {
            ScannerId = "linux-scheduled-task-scanner",
            Message = $"{source}: {status}",
            IsPermissionFailure = status == OperationStatus.AccessDenied
        });
    }

    /// <summary>Pure mapping, unit-testable against a synthetic CronEntry.</summary>
    internal static CoreScheduledTask BuildEntity(string sourceFile, int index, CronEntry entry, ISecretRedactor secretRedactor)
    {
        var executablePath = CronCommandPathExtractor.TryExtractExecutablePath(entry.Command);
        var redactedCommand = secretRedactor.Redact(entry.Command);

        var entity = new CoreScheduledTask
        {
            Id = $"scheduledtask:cron:{sourceFile}:{index}",
            Name = $"{sourceFile}#{index}",
            Type = "ScheduledTask",
            Source = EvidenceSources.Cron,
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Folder = sourceFile,
            Trigger = entry.Schedule,
            Action = executablePath ?? redactedCommand,
            RunAsAccount = entry.User,
            Enabled = true
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = sourceFile, Detail = $"line {index}" });

        entity.SetMetadata("Command", redactedCommand);
        if (secretRedactor.ContainsSecret(entry.Command))
        {
            entity.SetMetadata("SecretDetected", "true");
        }

        if (executablePath is null)
        {
            entity.SetMetadata("ExecutablePathStatus", "Unresolved");
        }

        return entity;
    }

    private static CoreScheduledTask BuildRunPartsEntity(string directory, string filePath, string trigger)
    {
        var entity = new CoreScheduledTask
        {
            Id = $"scheduledtask:cron:{filePath}",
            Name = System.IO.Path.GetFileName(filePath),
            Type = "ScheduledTask",
            Source = EvidenceSources.Cron,
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Folder = directory,
            Trigger = trigger,
            Action = filePath,
            Enabled = true
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = filePath, Detail = $"{trigger} run-parts entry" });

        return entity;
    }
}
