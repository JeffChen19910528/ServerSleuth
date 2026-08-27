using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>Detects installed .NET runtimes via `dotnet --list-runtimes` — never `dotnet
/// restore`/`dotnet run`/any executing subcommand. See skill.md §7, §22.</summary>
public sealed class DotNetRuntimeDetector(IExecutableLocator executableLocator, IProcessRunner processRunner) : IRuntimeDetector
{
    public string Id => "dotnet-runtime-detector";
    public string RuntimeFamily => "DotNetRuntime";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var dotnetPath = DotNetCliLocator.Locate(executableLocator);
        if (dotnetPath is null)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        const string command = "dotnet --list-runtimes";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = dotnetPath, Arguments = ["--list-runtimes"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return RuntimeDetectionResult.Partial([], $"'{command}' did not complete successfully ({result.Status}).");
        }

        var rows = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => DotNetCliLocator.ParseLine(line))
            .Where(parsed => parsed.Version is not null)
            .Select(parsed => new RuntimeDetectionRow
            {
                Family = RuntimeFamily,
                EntityKind = RuntimeEntityKind.Runtime,
                Name = parsed.Name ?? ".NET Runtime",
                Version = parsed.Version,
                InstallationPath = parsed.Path,
                ExecutablePath = dotnetPath,
                ExecutableAvailable = true,
                DetectionSources = ["Command"],
                Command = command
            })
            .ToList();

        return rows.Count > 0 ? RuntimeDetectionResult.Detected(rows) : RuntimeDetectionResult.NotDetected();
    }
}
