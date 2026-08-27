using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// Reads tasks via the native Task Scheduler 2.0 COM API (ProgID "Schedule.Service"), which
/// ships with every Windows install — never a third-party NuGet wrapper, and no compile-time
/// COM reference (avoids the same build-portability problem the IIS provider avoided in Phase
/// 4A: a machine building ServerSleuth.Windows doesn't need any extra component installed).
/// Read-only: only Connect/GetFolder/GetTasks/GetFolders and property getters are ever called
/// — never RegisterTask/Run/Stop/Delete/property setters. See skill.md §24 (strictly
/// read-only).
/// </summary>
public sealed class TaskSchedulerProvider(ILogger<TaskSchedulerProvider> logger) : ITaskSchedulerProvider
{
    private const int AccessDeniedHResult = unchecked((int)0x80070005);
    private const int IncludeHiddenTasksFlag = 1;
    private const int DefaultFolderEnumFlag = 0;

    public TaskSchedulerProbeResult GetSnapshot()
    {
        object taskService;
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service")
                ?? throw new InvalidOperationException("Schedule.Service COM ProgID not found.");
            taskService = Activator.CreateInstance(type)!;

            dynamic service = taskService;
            service.Connect();
        }
        catch (COMException ex) when (ex.HResult == AccessDeniedHResult)
        {
            return TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.AccessDenied, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to Task Scheduler.");
            return TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.Failed, ex.Message);
        }

        try
        {
            using var disposable = taskService as IDisposable;
            dynamic service = taskService;
            dynamic rootFolder = service.GetFolder(@"\");

            var tasks = new List<ScheduledTaskRow>();
            var partialFailures = new List<string>();
            Traverse(rootFolder, tasks, partialFailures);

            return TaskSchedulerProbeResult.Available(tasks, partialFailures);
        }
        catch (COMException ex) when (ex.HResult == AccessDeniedHResult)
        {
            return TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.AccessDenied, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate scheduled tasks.");
            return TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.Failed, ex.Message);
        }
    }

    private void Traverse(dynamic folder, List<ScheduledTaskRow> tasks, List<string> partialFailures)
    {
        foreach (dynamic task in folder.GetTasks(IncludeHiddenTasksFlag))
        {
            try
            {
                tasks.Add(ReadTask(task));
            }
            catch (Exception ex)
            {
                var path = TryGet(() => (string)task.Path, "<unknown>");
                partialFailures.Add($"Task '{path}': {ex.Message}");
            }
        }

        foreach (dynamic subFolder in folder.GetFolders(DefaultFolderEnumFlag))
        {
            try
            {
                Traverse(subFolder, tasks, partialFailures);
            }
            catch (Exception ex)
            {
                var path = TryGet(() => (string)subFolder.Path, "<unknown>");
                partialFailures.Add($"Folder '{path}': {ex.Message}");
            }
        }
    }

    private static ScheduledTaskRow ReadTask(dynamic task)
    {
        dynamic definition = task.Definition;

        var actions = new List<ScheduledTaskActionRow>();
        foreach (dynamic action in definition.Actions)
        {
            actions.Add(ReadAction(action));
        }

        var triggers = new List<ScheduledTaskTriggerRow>();
        foreach (dynamic trigger in definition.Triggers)
        {
            triggers.Add(ReadTrigger(trigger));
        }

        return new ScheduledTaskRow
        {
            Path = (string)task.Path,
            Name = (string)task.Name,
            Enabled = TryGet(() => (bool)task.Enabled, false),
            State = MapTaskState(TryGet(() => (int)task.State, -1)),
            Hidden = TryGet(() => (bool)definition.Settings.Hidden, false),
            Author = TryGet(() => (string)definition.RegistrationInfo.Author, null),
            Description = TryGet(() => (string)definition.RegistrationInfo.Description, null),
            RunLevel = TryGet(() => MapRunLevel((int)definition.Principal.RunLevel), null),
            UserId = TryGet(() => (string)definition.Principal.UserId, null),
            LastRunTime = TryGet<DateTimeOffset?>(() => task.LastRunTime, null),
            NextRunTime = TryGet<DateTimeOffset?>(() => task.NextRunTime, null),
            LastTaskResult = TryGet(() => (int?)task.LastTaskResult, null),
            ExecutionTimeLimit = TryGet(() => (string)definition.Settings.ExecutionTimeLimit, null),
            Actions = actions,
            Triggers = triggers
        };
    }

    private static ScheduledTaskActionRow ReadAction(dynamic action)
    {
        var typeCode = TryGet(() => (int)action.Type, -1);
        var type = MapActionType(typeCode);

        if (type != "Execute")
        {
            return new ScheduledTaskActionRow { Type = type };
        }

        return new ScheduledTaskActionRow
        {
            Type = type,
            Path = TryGet(() => (string)action.Path, null),
            Arguments = TryGet(() => (string)action.Arguments, null),
            WorkingDirectory = TryGet(() => (string)action.WorkingDirectory, null)
        };
    }

    private static ScheduledTaskTriggerRow ReadTrigger(dynamic trigger)
    {
        var typeCode = TryGet(() => (int)trigger.Type, -1);

        return new ScheduledTaskTriggerRow
        {
            Type = MapTriggerType(typeCode),
            Enabled = TryGet(() => (bool)trigger.Enabled, false),
            StartBoundary = TryGet(() => (string)trigger.StartBoundary, null),
            EndBoundary = TryGet(() => (string)trigger.EndBoundary, null)
        };
    }

    private static string MapTaskState(int state) => state switch
    {
        1 => "Disabled",
        2 => "Queued",
        3 => "Ready",
        4 => "Running",
        _ => "Unknown"
    };

    private static string MapActionType(int type) => type switch
    {
        0 => "Execute",
        5 => "ComHandler",
        6 => "Email",
        7 => "ShowMessage",
        _ => "Unknown"
    };

    private static string MapTriggerType(int type) => type switch
    {
        0 => "Event",
        1 => "Time",
        2 => "Daily",
        3 => "Weekly",
        4 => "Monthly",
        5 => "MonthlyDOW",
        6 => "Idle",
        7 => "Registration",
        8 => "Boot",
        9 => "Logon",
        11 => "SessionStateChange",
        _ => "Unknown"
    };

    private static string MapRunLevel(int runLevel) => runLevel switch
    {
        0 => "LeastPrivilege",
        1 => "HighestAvailable",
        _ => "Unknown"
    };

    private static T TryGet<T>(Func<T> accessor, T fallback)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return fallback;
        }
    }
}
