using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;

namespace ServerSleuth.Cli.Tests.Fakes;

/// <summary>Deterministically triggers the <see cref="ServerSleuth.Cli.ExitCodes.CliExitCode.GeneralFailure"/>
/// path — an unexpected, non-cancellation exception surfacing from a layer the CLI composes
/// (Phase 10B §8, §22-G: every defined exit code needs its own test).</summary>
internal sealed class ThrowingFakeDiscoveryEngine(Exception exception) : IDiscoveryEngine
{
    public Task<AggregateDiscoveryResult> RunAsync(DiscoveryContext context, CancellationToken cancellationToken) =>
        throw exception;
}
