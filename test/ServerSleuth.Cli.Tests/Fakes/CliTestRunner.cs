using ServerSleuth.Cli.Options;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Cli.Tests.Fakes;

internal static class CliTestRunner
{
    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string[] args,
        IDiscoveryEngine? discoveryEngine = null,
        CancellationToken cancellationToken = default,
        ITargetTransport? transport = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        IServiceProvider Factory(ScanOptions options) => discoveryEngine is null
            ? throw new InvalidOperationException("Test did not supply a fake IDiscoveryEngine.")
            : TestServiceProviderFactory.Build(discoveryEngine, transport);

        var app = new CliApplication(Factory, stdout, stderr);
        var exitCode = await app.RunAsync(args, cancellationToken);

        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
