using ServerSleuth.Linux.Common;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Linux.Runtimes.Detectors;

/// <summary>Detects Go via `go version` — never executes a Go program. Also records
/// GOROOT/GOPATH when set, redacted through ISecretRedactor first.</summary>
public sealed partial class GoDetector(IExecutableLocator executableLocator, IProcessRunner processRunner, ISecretRedactor secretRedactor)
    : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories = ["/usr/local/go/bin", "/usr/bin", "/opt/go/bin"];

    [GeneratedRegex(@"go(?<version>[\d.]+)")]
    private static partial Regex VersionPattern();

    public string Id => "linux-go-detector";
    public string RuntimeFamily => "Go";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var goPath = executableLocator.Locate("go", KnownDirectories);
        if (goPath is null)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        const string command = "go version";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = goPath, Arguments = ["version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return RuntimeDetectionResult.Partial([], $"'{command}' did not complete successfully ({result.Status}).");
        }

        var match = VersionPattern().Match(result.StandardOutput);
        var envVars = ReadEnvironmentVariables();

        var row = new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = "Go",
            Version = match.Success ? match.Groups["version"].Value : null,
            InstallationPath = LinuxPath.GetDirectoryName(LinuxPath.GetDirectoryName(goPath)), // .../go/bin/go -> .../go
            ExecutablePath = goPath,
            ExecutableAvailable = true,
            DetectionSources = ["Command"],
            Command = command,
            EnvironmentVariables = envVars
        };

        return RuntimeDetectionResult.Detected([row]);
    }

    private Dictionary<string, string> ReadEnvironmentVariables()
    {
        var result = new Dictionary<string, string>();

        foreach (var name in new[] { "GOROOT", "GOPATH" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value is not null)
            {
                result[name] = secretRedactor.Redact(value);
            }
        }

        return result;
    }
}
