namespace ServerSleuth.Gui.ExecutionHost.Tests.Fakes;

/// <summary>A deterministic <see cref="IProgress{T}"/> — unlike the BCL's own
/// <see cref="Progress{T}"/> (which marshals each report via a captured
/// <see cref="System.Threading.SynchronizationContext"/>, or a <c>ThreadPool</c> work item when
/// none exists — introducing ordering nondeterminism in a plain xUnit test), this reports
/// synchronously and inline, so a test's captured list is guaranteed complete and in order the
/// instant <c>ExecuteAsync</c> returns.</summary>
internal sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
{
    public void Report(T value) => onReport(value);
}
