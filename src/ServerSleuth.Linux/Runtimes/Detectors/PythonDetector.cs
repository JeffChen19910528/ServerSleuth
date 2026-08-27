using ServerSleuth.Linux.Common;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Runtimes;

namespace ServerSleuth.Linux.Runtimes.Detectors;

/// <summary>
/// Detects every installed Python interpreter — not just the one PATH happens to resolve
/// first. Linux distributions commonly ship multiple coexisting `python3`, `python3.10`,
/// `python3.11`, etc. binaries side by side; each distinct resolved executable path becomes
/// its own row, never merged (skill.md (Phase 6B) §14).
/// </summary>
public sealed partial class PythonDetector(
    IExecutableLocator executableLocator,
    IProcessRunner processRunner,
    IFileSystemReader fileSystemReader) : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownDirectories = ["/usr/bin", "/usr/local/bin", "/opt/python/bin"];

    [GeneratedRegex(@"Python (?<version>[\d.]+)")]
    private static partial Regex VersionPattern();

    public string Id => "linux-python-detector";
    public string RuntimeFamily => "Python";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var candidatePaths = new HashSet<string>(StringComparer.Ordinal);

        candidatePaths.UnionWith(ScanKnownDirectories());

        var direct = executableLocator.Locate("python3", []);
        if (direct is not null)
        {
            candidatePaths.Add(direct);
        }

        if (candidatePaths.Count == 0)
        {
            return RuntimeDetectionResult.NotDetected();
        }

        var rows = new List<RuntimeDetectionRow>();
        foreach (var path in candidatePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = await BuildRow(path, cancellationToken);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows.Count > 0 ? RuntimeDetectionResult.Detected(rows) : RuntimeDetectionResult.NotDetected();
    }

    private async Task<RuntimeDetectionRow?> BuildRow(string path, CancellationToken cancellationToken)
    {
        const string command = "python3 --version";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = path, Arguments = ["--version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return null; // resolved a path that couldn't actually run — not a valid interpreter
        }

        var output = result.StandardOutput.Length > 0 ? result.StandardOutput : result.StandardError;
        var match = VersionPattern().Match(output);

        return new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = "Python",
            Version = match.Success ? match.Groups["version"].Value : null,
            InstallationPath = LinuxPath.GetDirectoryName(path),
            ExecutablePath = path,
            ExecutableAvailable = true,
            DetectionSources = ["Command"],
            Command = command
        };
    }

    private IEnumerable<string> ScanKnownDirectories()
    {
        var found = new List<string>();

        foreach (var directory in KnownDirectories)
        {
            var filesResult = fileSystemReader.EnumerateFiles(directory, "python3*");
            if (!filesResult.Success)
            {
                continue;
            }

            found.AddRange(filesResult.Value!);
        }

        return found;
    }
}
