using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Packages;

/// <summary>
/// Alpine package discovery via `apk info -v` — never `apk add`/`del`/`upgrade`. See skill.md
/// (Phase 6B) §3-4. `apk` has no per-field query-format option like dpkg/rpm, so name/version
/// are split via <see cref="ApkPackageLineParser"/>'s Alpine-specific "-r&lt;release&gt;"
/// convention rather than guessed.
/// </summary>
public sealed class ApkPackageProvider(IProcessRunner processRunner) : IPackageManagerProvider
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    public string PackageManagerName => "apk";

    public async Task<PackageQueryResult> QueryInstalledPackagesAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = "apk", Arguments = ["info", "-v"], Timeout = CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            var status = result.Status == OperationStatus.StartFailed
                ? PackageManagerAvailability.NotInstalled
                : PackageManagerAvailability.Failed;

            return new PackageQueryResult { Status = status, ErrorMessage = result.StandardError };
        }

        var packages = new List<PackageRow>();

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parsed = ApkPackageLineParser.Parse(line);
            if (parsed is null)
            {
                continue; // unrecognized shape — skipped, never guessed at
            }

            packages.Add(new PackageRow { Name = parsed.Value.Name, Version = parsed.Value.Version });
        }

        return new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = packages };
    }
}
