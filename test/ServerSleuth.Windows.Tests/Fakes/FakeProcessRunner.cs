using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Windows.Tests.Fakes;

/// <summary>Keyed by "{Executable}|{arg1} {arg2} ..." so tests can script exactly what a
/// given command line returns without touching a real process.</summary>
internal sealed class FakeProcessRunner(Dictionary<string, ProcessResult> byCommand) : IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var key = $"{request.Executable}|{string.Join(' ', request.Arguments)}";
        return Task.FromResult(byCommand.GetValueOrDefault(key, ProcessResult.StartFailedResult(TimeSpan.Zero)));
    }
}
