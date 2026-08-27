using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Packages;

/// <summary>
/// Debian/Ubuntu package discovery via `dpkg-query -W` with a fixed, machine-readable format
/// string — never `apt`/`apt-get` (which can trigger repository refreshes), and never
/// `install`/`upgrade`/`remove`. See skill.md (Phase 6B) §3-4.
/// </summary>
public sealed class DpkgPackageProvider(IProcessRunner processRunner) : IPackageManagerProvider
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);
    private const string FormatString = "-f=${Package}\t${Version}\t${Architecture}\t${Maintainer}\n";

    public string PackageManagerName => "dpkg";

    public async Task<PackageQueryResult> QueryInstalledPackagesAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = "dpkg-query", Arguments = ["-W", FormatString], Timeout = CommandTimeout },
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
            var fields = line.Split('\t');
            if (fields.Length != 4)
            {
                continue; // malformed line — skipped, never guessed at
            }

            packages.Add(new PackageRow
            {
                Name = fields[0],
                Version = NullIfEmpty(fields[1]),
                Architecture = NullIfEmpty(fields[2]),
                Maintainer = NullIfEmpty(fields[3])
            });
        }

        return new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = packages };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
