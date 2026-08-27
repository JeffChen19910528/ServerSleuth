using ServerSleuth.Infrastructure.Runtimes;
using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>
/// Detects every installed Python interpreter — not just the one PATH happens to resolve
/// first. Uses the `py` launcher's `-0p` listing (the most reliable multi-version Python
/// detection mechanism on Windows) plus a scan of known per-version install directories, then
/// confirms each unique interpreter with `--version`. See skill.md §10, §17 (every version
/// represented independently).
/// </summary>
public sealed class PythonDetector(
    IExecutableLocator executableLocator,
    IProcessRunner processRunner,
    IFileSystemReader fileSystemReader) : IRuntimeDetector
{
    private static readonly IReadOnlyList<string> KnownParentDirectories =
    [
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Python"
    ];

    private static readonly Regex PathLinePattern = new(@"(?<path>[A-Za-z]:\\[^\r\n]*?python3?\.exe)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionPattern = new(@"Python (?<version>[\d.]+)", RegexOptions.Compiled);

    public string Id => "python-detector";
    public string RuntimeFamily => "Python";

    public async Task<RuntimeDetectionResult> DetectAsync(CancellationToken cancellationToken)
    {
        var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        candidatePaths.UnionWith(await ListViaPyLauncher(cancellationToken));
        candidatePaths.UnionWith(ScanKnownDirectories());

        var direct = executableLocator.Locate("python.exe", []) ?? executableLocator.Locate("python3.exe", []);
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
        const string command = "python --version";
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = path, Arguments = ["--version"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return null; // resolved a path that couldn't actually run — not a valid interpreter
        }

        var output = result.StandardOutput.Length > 0 ? result.StandardOutput : result.StandardError;
        var match = VersionPattern.Match(output);

        return new RuntimeDetectionRow
        {
            Family = RuntimeFamily,
            EntityKind = RuntimeEntityKind.Runtime,
            Name = "Python",
            Version = match.Success ? match.Groups["version"].Value : null,
            InstallationPath = Path.GetDirectoryName(path),
            ExecutablePath = path,
            ExecutableAvailable = true,
            Architecture = ArchitectureHint.FromPath(path),
            DetectionSources = ["Command"],
            Command = command
        };
    }

    private async Task<IEnumerable<string>> ListViaPyLauncher(CancellationToken cancellationToken)
    {
        var pyPath = executableLocator.Locate("py.exe", [@"C:\Windows"]);
        if (pyPath is null)
        {
            return [];
        }

        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = pyPath, Arguments = ["-0p"], Timeout = RuntimeDetectionDefaults.CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return [];
        }

        return PathLinePattern.Matches(result.StandardOutput).Select(m => m.Groups["path"].Value.Trim());
    }

    private IEnumerable<string> ScanKnownDirectories()
    {
        var found = new List<string>();

        foreach (var parent in KnownParentDirectories)
        {
            var subDirsResult = fileSystemReader.EnumerateDirectories(parent, "Python*");
            if (!subDirsResult.Success)
            {
                continue;
            }

            foreach (var subDir in subDirsResult.Value!)
            {
                var candidate = Path.Combine(subDir, "python.exe");
                if (fileSystemReader.Exists(candidate))
                {
                    found.Add(candidate);
                }
            }
        }

        return found;
    }
}
