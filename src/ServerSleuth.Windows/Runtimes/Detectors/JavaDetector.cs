using ServerSleuth.Infrastructure.Runtimes;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Windows.Registry;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>
/// Detects Java (JRE/JDK) via both the JavaSoft registry keys (JDK/JRE for Java 9+, the older
/// "Java Development Kit"/"Java Runtime Environment" keys for 8 and earlier) and a command-based
/// `java -version` check. When both sources resolve to the same JavaHome, they are merged into
/// one row (with a ConflictNote if the versions disagree) rather than reported twice — but a
/// registry entry with no matching executable is still reported, not discarded (skill.md §25).
/// </summary>
public sealed class JavaDetector(
    IWindowsRegistryReader registryReader,
    IExecutableLocator executableLocator,
    IProcessRunner processRunner,
    IFileSystemReader fileSystemReader) : IRuntimeDetector
{
    private static readonly (string RootKey, bool IsJdk)[] RegistryRoots =
    [
        (@"SOFTWARE\JavaSoft\JDK", true),
        (@"SOFTWARE\JavaSoft\JRE", false),
        (@"SOFTWARE\JavaSoft\Java Development Kit", true),
        (@"SOFTWARE\JavaSoft\Java Runtime Environment", false)
    ];

    private static readonly IReadOnlyList<string> KnownDirectories =
    [
        @"C:\Program Files\Java",
        @"C:\Program Files\Eclipse Adoptium",
        @"C:\Program Files\Microsoft\jdk",
        @"C:\Program Files\Zulu",
        @"C:\Program Files\Amazon Corretto"
    ];

    private static readonly Regex VersionPattern = new("version \"(?<version>[^\"]+)\"", RegexOptions.Compiled);

    public string Id => "java-detector";
    public string RuntimeFamily => "Java";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var registryRows = ReadRegistryRows();
        var commandRow = await TryReadCommandRow(cancellationToken);

        var rows = Merge(registryRows, commandRow);

        return rows.Count > 0 ? RuntimeDetectionResult.Detected(rows) : RuntimeDetectionResult.NotDetected();
    }

    private List<RuntimeDetectionRow> ReadRegistryRows()
    {
        var rows = new List<RuntimeDetectionRow>();

        foreach (var (rootKey, isJdk) in RegistryRoots)
        {
            var versionNames = registryReader.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, rootKey);
            if (!versionNames.Success)
            {
                continue;
            }

            foreach (var versionName in versionNames.Value!)
            {
                var values = registryReader.GetValues(RegistryHive.LocalMachine, RegistryView.Registry64, $@"{rootKey}\{versionName}");
                if (!values.Success)
                {
                    continue;
                }

                var javaHome = values.Value!.GetValueOrDefault("JavaHome") as string;

                rows.Add(new RuntimeDetectionRow
                {
                    Family = RuntimeFamily,
                    EntityKind = RuntimeEntityKind.Runtime,
                    Name = isJdk ? "Java (JDK)" : "Java (JRE)",
                    Version = versionName,
                    InstallationPath = javaHome,
                    Architecture = ArchitectureHint.FromPath(javaHome),
                    DetectionSources = ["Registry"],
                    RegistryPath = $@"HKLM\{rootKey}\{versionName}"
                });
            }
        }

        return rows;
    }

    private async Task<RuntimeDetectionRow?> TryReadCommandRow(CancellationToken cancellationToken)
    {
        var javaPath = executableLocator.Locate("java.exe", KnownDirectories.SelectMany(ExpandJavaSubdirectories).ToList());
        if (javaPath is null)
        {
            return null;
        }

        const string command = "java -version";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = javaPath, Arguments = ["-version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        // java -version writes to stderr by convention; some distributions write to stdout.
        var output = result.StandardError.Length > 0 ? result.StandardError : result.StandardOutput;
        var match = VersionPattern.Match(output);
        if (!match.Success)
        {
            return null;
        }

        var binDirectory = Path.GetDirectoryName(javaPath) ?? string.Empty;
        var javaHome = Path.GetDirectoryName(binDirectory); // .../<JavaHome>/bin/java.exe
        var isJdk = fileSystemReader.Exists(Path.Combine(binDirectory, "javac.exe"));
        var vendor = DetectVendor(output);

        return new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = isJdk ? "Java (JDK)" : "Java (JRE)",
            Version = match.Groups["version"].Value,
            Edition = vendor,
            InstallationPath = javaHome,
            ExecutablePath = javaPath,
            ExecutableAvailable = true,
            Architecture = ArchitectureHint.FromPath(javaHome),
            DetectionSources = ["Command"],
            Command = command
        };
    }

    private static string? DetectVendor(string versionOutput)
    {
        if (versionOutput.Contains("Eclipse Adoptium", StringComparison.OrdinalIgnoreCase) || versionOutput.Contains("Temurin", StringComparison.OrdinalIgnoreCase)) return "Eclipse Temurin";
        if (versionOutput.Contains("Zulu", StringComparison.OrdinalIgnoreCase)) return "Azul Zulu";
        if (versionOutput.Contains("Corretto", StringComparison.OrdinalIgnoreCase)) return "Amazon Corretto";
        if (versionOutput.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return "Microsoft Build of OpenJDK";
        if (versionOutput.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase)) return "OpenJDK";
        if (versionOutput.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase) || versionOutput.Contains("HotSpot", StringComparison.OrdinalIgnoreCase)) return "Oracle";
        return null; // never guessed beyond explicit textual markers
    }

    private static IEnumerable<string> ExpandJavaSubdirectories(string root) =>
        [root, Path.Combine(root, "bin")]; // covers both "<root>\jdk-17\bin" style layouts already resolved and a bare root

    private static List<RuntimeDetectionRow> Merge(List<RuntimeDetectionRow> registryRows, RuntimeDetectionRow? commandRow)
    {
        if (commandRow is null)
        {
            return registryRows;
        }

        var matchIndex = registryRows.FindIndex(r =>
            r.InstallationPath is not null && commandRow.InstallationPath is not null &&
            string.Equals(NormalizePath(r.InstallationPath), NormalizePath(commandRow.InstallationPath), StringComparison.OrdinalIgnoreCase));

        if (matchIndex < 0)
        {
            registryRows.Add(commandRow);
            return registryRows;
        }

        var registryRow = registryRows[matchIndex];
        var conflictNote = registryRow.Version != commandRow.Version
            ? $"Registry reports {registryRow.Version}, executable reports {commandRow.Version}."
            : null;

        registryRows[matchIndex] = registryRow with
        {
            Version = commandRow.Version, // executable output is authoritative when both agree on identity
            Edition = commandRow.Edition ?? registryRow.Edition,
            ExecutablePath = commandRow.ExecutablePath,
            ExecutableAvailable = true,
            DetectionSources = [.. registryRow.DetectionSources, .. commandRow.DetectionSources],
            Command = commandRow.Command,
            ConflictNote = conflictNote
        };

        return registryRows;
    }

    private static string NormalizePath(string path) => path.TrimEnd('\\').ToLowerInvariant();
}
