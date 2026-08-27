using System.Diagnostics;

namespace ServerSleuth.Analysis.Tests.Diagnostics;

/// <summary>
/// Wraps an existing pipeline call with a <see cref="Stopwatch"/> and an optional diagnostic-
/// only observation timeout — see skill.md (Phase 10A-H) §2, §10. Never changes what the wrapped
/// call does or returns; on timeout the underlying <see cref="Task.Run(Action)"/> is simply
/// abandoned (this is a one-off diagnostic test process, not production code — skill.md §10
/// explicitly permits terminating only the diagnostic process itself, never a scanned server
/// process, and abandoning an orphaned background thread until process exit satisfies that
/// without introducing any thread-abort/kill logic).
/// </summary>
internal static class StageMeasurement
{
    public static (T? Result, double ElapsedMs, bool TimedOut) Measure<T>(Func<T> action, TimeSpan? timeout = null)
    {
        var sw = Stopwatch.StartNew();

        if (timeout is null)
        {
            var result = action();
            sw.Stop();
            return (result, sw.Elapsed.TotalMilliseconds, false);
        }

        var task = Task.Run(action);
        var completed = task.Wait(timeout.Value);
        sw.Stop();

        return completed ? (task.Result, sw.Elapsed.TotalMilliseconds, false) : (default, sw.Elapsed.TotalMilliseconds, true);
    }
}
