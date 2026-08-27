using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Common;
using CoreScheduledTask = ServerSleuth.Core.Models.ScheduledTask;

namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// Discovers Windows Scheduled Tasks via the Task Scheduler 2.0 COM API. A disabled task is
/// still recorded (and never treated as "unused" — that determination is Phase 5's job, not
/// this scanner's). See skill.md §14, §10.
/// </summary>
public sealed class WindowsScheduledTaskScanner(
    ITaskSchedulerProvider provider,
    IFileSystemReader fileSystemReader,
    ISecretRedactor secretRedactor,
    ILogger<WindowsScheduledTaskScanner> logger) : IDiscoveryScanner
{
    public string Id => "windows-scheduled-task-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var probe = provider.GetSnapshot();

        var result = probe.Status switch
        {
            TaskSchedulerAvailability.AccessDenied => new DiscoveryResult
            {
                ScannerId = Id,
                Status = ScannerStatus.AccessDenied,
                Errors = [new DiscoveryError { ScannerId = Id, Message = probe.ErrorMessage ?? "Access denied.", IsPermissionFailure = true }]
            },
            TaskSchedulerAvailability.Failed => DiscoveryResult.Failure(Id, new DiscoveryError { ScannerId = Id, Message = probe.ErrorMessage ?? "Task Scheduler enumeration failed." }),
            _ => BuildResult(probe)
        };

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} tasks", Id, result.Entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        return Task.FromResult(result);
    }

    private DiscoveryResult BuildResult(TaskSchedulerProbeResult probe)
    {
        var entities = probe.Tasks.Select(row => BuildEntity(row, secretRedactor, fileSystemReader)).ToList();
        var errors = probe.PartialFailures.Select(f => new DiscoveryError { ScannerId = Id, Message = f, IsPermissionFailure = true }).ToList();
        var status = errors.Count > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    /// <summary>Pure mapping aside from the optional file-existence checks, unit-testable
    /// against a synthetic ScheduledTaskRow (pass fileSystemReader = null to skip those).</summary>
    internal static CoreScheduledTask BuildEntity(ScheduledTaskRow row, ISecretRedactor secretRedactor, IFileSystemReader? fileSystemReader = null)
    {
        var primaryAction = row.Actions.FirstOrDefault(a => a.Type == "Execute") ?? row.Actions.FirstOrDefault();
        var primaryTrigger = row.Triggers.FirstOrDefault();
        var lastSeparator = row.Path.LastIndexOf('\\');

        var entity = new CoreScheduledTask
        {
            Id = $"scheduledtask:{row.Path}",
            Name = row.Name,
            Type = "ScheduledTask",
            Source = EvidenceSources.WindowsTaskScheduler,
            Status = row.State == "Running" ? EntityStatus.Running : EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Folder = lastSeparator > 0 ? row.Path[..lastSeparator] : @"\",
            Trigger = primaryTrigger?.Type,
            NextRun = row.NextRunTime,
            Action = primaryAction?.Path ?? primaryAction?.Type,
            RunAsAccount = row.UserId,
            Enabled = row.Enabled
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.ScheduledTask, Location = row.Path, Detail = primaryAction?.Path });

        entity.SetMetadata("State", row.State);
        entity.SetMetadata("Hidden", row.Hidden.ToString());

        if (row.Author is not null) entity.SetMetadata("Author", row.Author);
        if (row.Description is not null) SetRedactedMetadata(entity, secretRedactor, "Description", row.Description);
        if (row.RunLevel is not null) entity.SetMetadata("RunLevel", row.RunLevel);
        if (row.LastRunTime is not null) entity.SetMetadata("LastRunTime", row.LastRunTime.Value.ToString("O"));
        if (row.LastTaskResult is not null) entity.SetMetadata("LastTaskResult", $"0x{row.LastTaskResult.Value:X8}");
        if (row.ExecutionTimeLimit is not null) entity.SetMetadata("ExecutionTimeLimit", row.ExecutionTimeLimit);

        entity.SetMetadata("ActionCount", row.Actions.Count.ToString());
        entity.SetMetadata("TriggerCount", row.Triggers.Count.ToString());

        for (var i = 0; i < row.Actions.Count; i++)
        {
            var action = row.Actions[i];
            entity.SetMetadata($"Action{i}.Type", action.Type);
            if (action.Path is not null) entity.SetMetadata($"Action{i}.Path", action.Path);
            if (action.Arguments is not null) SetRedactedMetadata(entity, secretRedactor, $"Action{i}.Arguments", action.Arguments);
            if (action.WorkingDirectory is not null) entity.SetMetadata($"Action{i}.WorkingDirectory", action.WorkingDirectory);

            if (ScriptPathExtractor.IsScriptHost(action.Path))
            {
                var scriptPath = ScriptPathExtractor.TryExtract(action.Arguments);
                if (scriptPath is not null)
                {
                    entity.SetMetadata($"Action{i}.ScriptPath", scriptPath);
                    ApplyPathVerification(entity, $"Action{i}.ScriptPathStatus", scriptPath, fileSystemReader);
                }
            }
        }

        for (var i = 0; i < row.Triggers.Count; i++)
        {
            var trigger = row.Triggers[i];
            entity.SetMetadata($"Trigger{i}.Type", trigger.Type);
            entity.SetMetadata($"Trigger{i}.Enabled", trigger.Enabled.ToString());
            if (trigger.StartBoundary is not null) entity.SetMetadata($"Trigger{i}.StartBoundary", trigger.StartBoundary);
            if (trigger.EndBoundary is not null) entity.SetMetadata($"Trigger{i}.EndBoundary", trigger.EndBoundary);
        }

        if (primaryAction?.Type == "Execute" && primaryAction.Path is not null)
        {
            ApplyPathVerification(entity, "ActionExecutableStatus", primaryAction.Path, fileSystemReader);
        }

        return entity;
    }

    private static void SetRedactedMetadata(CoreScheduledTask entity, ISecretRedactor secretRedactor, string key, string rawValue)
    {
        if (secretRedactor.ContainsSecret(rawValue))
        {
            entity.SetMetadata("SecretDetected", "true");
        }

        entity.SetMetadata(key, secretRedactor.Redact(rawValue));
    }

    private static void ApplyPathVerification(CoreScheduledTask entity, string metadataKey, string path, IFileSystemReader? fileSystemReader)
    {
        if (fileSystemReader is null)
        {
            return;
        }

        var infoResult = fileSystemReader.GetFileInfo(path);
        if (!infoResult.Success)
        {
            entity.SetMetadata(metadataKey, infoResult.Status switch
            {
                OperationStatus.AccessDenied => "AccessDenied",
                OperationStatus.NotFound => "NotFound",
                _ => "Unavailable"
            });
        }
    }
}
