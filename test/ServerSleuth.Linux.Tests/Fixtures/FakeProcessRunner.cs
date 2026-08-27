using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Tests.Fixtures;

/// <summary>Deterministic fake of IProcessRunner — no real process execution.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, ProcessResult> _results = new(StringComparer.Ordinal);
    private readonly List<ProcessRequest> _invocations = [];

    /// <summary>Every request actually passed to <see cref="RunAsync"/>, in call order — used
    /// by negative security tests to assert a forbidden command/argument was never invoked.</summary>
    public IReadOnlyList<ProcessRequest> Invocations => _invocations;

    public void SetResult(string executable, IReadOnlyList<string> arguments, ProcessResult result) =>
        _results[Key(executable, arguments)] = result;

    public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        _invocations.Add(request);

        var key = Key(request.Executable, request.Arguments);
        return Task.FromResult(_results.TryGetValue(key, out var result)
            ? result
            : ProcessResult.StartFailedResult(TimeSpan.Zero));
    }

    private static string Key(string executable, IReadOnlyList<string> arguments) =>
        $"{executable} {string.Join(' ', arguments)}";
}
