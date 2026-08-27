using ServerSleuth.Linux.Common;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Runtimes;

namespace ServerSleuth.Linux.Runtimes.Detectors;

/// <summary>Detects Node.js and npm independently — never `npm install` or any package
/// script.</summary>
public sealed class NodeDetector(IExecutableLocator executableLocator, IProcessRunner processRunner) : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories = ["/usr/bin", "/usr/local/bin", "/opt/nodejs/bin"];

    public string Id => "linux-node-detector";
    public string RuntimeFamily => "Node";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var rows = new List<RuntimeDetectionRow>();

        var nodePath = executableLocator.Locate("node", KnownDirectories);
        if (nodePath is not null)
        {
            var row = await RunVersionQuery(nodePath, "node --version", "Node.js", "Node", cancellationToken);
            if (row is not null) rows.Add(row);
        }

        var npmPath = executableLocator.Locate("npm", KnownDirectories);
        if (npmPath is not null)
        {
            var row = await RunVersionQuery(npmPath, "npm --version", "npm", "Npm", cancellationToken);
            if (row is not null) rows.Add(row);
        }

        return rows.Count > 0 ? RuntimeDetectionResult.Detected(rows) : RuntimeDetectionResult.NotDetected();
    }

    private async Task<RuntimeDetectionRow?> RunVersionQuery(string executablePath, string command, string name, string family, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = executablePath, Arguments = ["--version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return null;
        }

        var version = result.StandardOutput.Trim().TrimStart('v');

        return new RuntimeDetectionRow
        {
            Family = family,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = name,
            Version = version.Length > 0 ? version : null,
            InstallationPath = LinuxPath.GetDirectoryName(executablePath),
            ExecutablePath = executablePath,
            ExecutableAvailable = true,
            DetectionSources = ["Command"],
            Command = command
        };
    }
}
