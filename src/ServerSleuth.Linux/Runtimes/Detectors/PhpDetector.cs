using ServerSleuth.Linux.Common;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Runtimes;

namespace ServerSleuth.Linux.Runtimes.Detectors;

/// <summary>Detects PHP CLI via `php --version` — never executes a PHP script.</summary>
public sealed partial class PhpDetector(IExecutableLocator executableLocator, IProcessRunner processRunner) : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories = ["/usr/bin", "/usr/local/bin", "/opt/php/bin"];

    [GeneratedRegex(@"PHP (?<version>[\d.]+)")]
    private static partial Regex VersionPattern();

    public string Id => "linux-php-detector";
    public string RuntimeFamily => "Php";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var phpPath = executableLocator.Locate("php", KnownDirectories);
        if (phpPath is null)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        const string command = "php --version";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = phpPath, Arguments = ["--version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return RuntimeDetectionResult.Partial([], $"'{command}' did not complete successfully ({result.Status}).");
        }

        var match = VersionPattern().Match(result.StandardOutput);

        var row = new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = "PHP",
            Version = match.Success ? match.Groups["version"].Value : null,
            InstallationPath = LinuxPath.GetDirectoryName(phpPath),
            ExecutablePath = phpPath,
            ExecutableAvailable = true,
            DetectionSources = ["Command"],
            Command = command
        };

        return RuntimeDetectionResult.Detected([row]);
    }
}
