using ServerSleuth.Linux.Common;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Runtimes;

namespace ServerSleuth.Linux.Runtimes.Detectors;

/// <summary>Detects Java (JRE/JDK) via `java -version` — never a build/run subcommand. Linux
/// has no registry, so this is command-only (unlike Windows's registry+command merge).</summary>
public sealed partial class JavaDetector(IExecutableLocator executableLocator, IProcessRunner processRunner, IFileSystemReader fileSystemReader)
    : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories =
    [
        "/usr/lib/jvm/default-java/bin",
        "/usr/lib/jvm/java-17-openjdk/bin",
        "/usr/lib/jvm/java-21-openjdk/bin",
        "/opt/java/bin"
    ];

    [GeneratedRegex("version \"(?<version>[^\"]+)\"")]
    private static partial Regex VersionPattern();

    public string Id => "linux-java-detector";
    public string RuntimeFamily => "Java";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var javaPath = executableLocator.Locate("java", KnownDirectories);
        if (javaPath is null)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        const string command = "java -version";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = javaPath, Arguments = ["-version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return RuntimeDetectionResult.Partial([], $"'{command}' did not complete successfully ({result.Status}).");
        }

        // java -version writes to stderr by convention; some distributions write to stdout.
        var output = result.StandardError.Length > 0 ? result.StandardError : result.StandardOutput;
        var match = VersionPattern().Match(output);
        if (!match.Success)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        var binDirectory = LinuxPath.GetDirectoryName(javaPath) ?? string.Empty;
        var javaHome = LinuxPath.GetDirectoryName(binDirectory);
        var isJdk = fileSystemReader.Exists($"{binDirectory.TrimEnd('/')}/javac");
        var vendor = DetectVendor(output);

        var row = new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime, // matches Windows JavaDetector's convention — JDK vs JRE is reflected in Name, not EntityKind
            Name = isJdk ? "Java (JDK)" : "Java (JRE)",
            Version = match.Groups["version"].Value,
            Edition = vendor,
            InstallationPath = javaHome,
            ExecutablePath = javaPath,
            ExecutableAvailable = true,
            DetectionSources = ["Command"],
            Command = command
        };

        return RuntimeDetectionResult.Detected([row]);
    }

    private static string? DetectVendor(string versionOutput)
    {
        if (versionOutput.Contains("Eclipse Adoptium", StringComparison.OrdinalIgnoreCase) || versionOutput.Contains("Temurin", StringComparison.OrdinalIgnoreCase)) return "Eclipse Temurin";
        if (versionOutput.Contains("Zulu", StringComparison.OrdinalIgnoreCase)) return "Azul Zulu";
        if (versionOutput.Contains("Corretto", StringComparison.OrdinalIgnoreCase)) return "Amazon Corretto";
        if (versionOutput.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return "Microsoft Build of OpenJDK";
        if (versionOutput.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase)) return "OpenJDK";
        return null; // never guessed beyond explicit textual markers
    }
}
