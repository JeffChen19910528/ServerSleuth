using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>Detects installed .NET SDKs via `dotnet --list-sdks`. Never reported as a Runtime
/// entity — an SDK's presence is not runtime evidence. See skill.md §8, §18.</summary>
public sealed class DotNetSdkDetector(IExecutableLocator executableLocator, IProcessRunner processRunner) : IRuntimeDetector
{
    public string Id => "dotnet-sdk-detector";
    public string RuntimeFamily => "DotNetSdk";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var dotnetPath = DotNetCliLocator.Locate(executableLocator);
        if (dotnetPath is null)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        const string command = "dotnet --list-sdks";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = dotnetPath, Arguments = ["--list-sdks"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
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
                EntityKind = RuntimeEntityKind.Sdk,
                Name = ".NET SDK",
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
