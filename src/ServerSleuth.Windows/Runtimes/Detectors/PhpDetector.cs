using ServerSleuth.Infrastructure.Runtimes;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>Detects PHP CLI via `php --version` — never executes a PHP script. See skill.md §12.</summary>
public sealed class PhpDetector(IExecutableLocator executableLocator, IProcessRunner processRunner) : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories =
    [
        @"C:\php",
        @"C:\Program Files\PHP",
        @"C:\xampp\php"
    ];

    private static readonly Regex VersionPattern = new(@"PHP (?<version>[\d.]+)", RegexOptions.Compiled);

    public string Id => "php-detector";
    public string RuntimeFamily => "Php";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var phpPath = executableLocator.Locate("php.exe", KnownDirectories);
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

        var match = VersionPattern.Match(result.StandardOutput);

        var row = new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = "PHP",
            Version = match.Success ? match.Groups["version"].Value : null,
            InstallationPath = Path.GetDirectoryName(phpPath),
            ExecutablePath = phpPath,
            ExecutableAvailable = true,
            Architecture = ArchitectureHint.FromPath(phpPath),
            DetectionSources = ["Command"],
            Command = command
        };

        return RuntimeDetectionResult.Detected([row]);
    }
}
