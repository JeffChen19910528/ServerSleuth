using ServerSleuth.Infrastructure.Runtimes;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>Detects Go via `go version` — never executes a Go program. Also records
/// GOROOT/GOPATH when set, redacted through ISecretRedactor first. See skill.md §13, §19.</summary>
public sealed class GoDetector(IExecutableLocator executableLocator, IProcessRunner processRunner, ISecretRedactor secretRedactor) : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories =
    [
        @"C:\Go\bin",
        @"C:\Program Files\Go\bin"
    ];

    private static readonly Regex VersionPattern = new(@"go(?<version>[\d.]+)", RegexOptions.Compiled);

    public string Id => "go-detector";
    public string RuntimeFamily => "Go";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var goPath = executableLocator.Locate("go.exe", KnownDirectories);
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

        var match = VersionPattern.Match(result.StandardOutput);
        var envVars = ReadEnvironmentVariables();

        var row = new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = "Go",
            Version = match.Success ? match.Groups["version"].Value : null,
            InstallationPath = Path.GetDirectoryName(Path.GetDirectoryName(goPath)), // .../go/bin/go.exe -> .../go
            ExecutablePath = goPath,
            ExecutableAvailable = true,
            Architecture = ArchitectureHint.FromPath(goPath),
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
