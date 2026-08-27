using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Packages;

/// <summary>
/// RPM-based (RHEL/Fedora/openSUSE/etc.) package discovery via `rpm -qa --queryformat` with a
/// fixed, machine-readable format — never `dnf`/`yum`/`zypper install|upgrade|remove`. See
/// skill.md (Phase 6B) §3-4.
/// </summary>
public sealed class RpmPackageProvider(IProcessRunner processRunner) : IPackageManagerProvider
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);
    private const string FormatString = "%{NAME}\t%{VERSION}-%{RELEASE}\t%{ARCH}\t%{VENDOR}\t%{SOURCERPM}\n";

    public string PackageManagerName => "rpm";

    public async Task<PackageQueryResult> QueryInstalledPackagesAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = "rpm", Arguments = ["-qa", "--queryformat", FormatString], Timeout = CommandTimeout },
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
            if (fields.Length != 5)
            {
                continue; // malformed line — skipped, never guessed at
            }

            packages.Add(new PackageRow
            {
                Name = fields[0],
                Version = NullIfEmptyOrNone(fields[1]),
                Architecture = NullIfEmptyOrNone(fields[2]),
                Maintainer = NullIfEmptyOrNone(fields[3]),
                SourcePackage = NullIfEmptyOrNone(fields[4])
            });
        }

        return new PackageQueryResult { Status = PackageManagerAvailability.Available, Packages = packages };
    }

    // rpm reports "(none)" for a field with no recorded value — never treated as real data.
    private static string? NullIfEmptyOrNone(string value) =>
        string.IsNullOrWhiteSpace(value) || value == "(none)" ? null : value;
}
