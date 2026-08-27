using System.Text.RegularExpressions;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Native;

/// <summary>
/// Reads the dynamic linker's cache via `ldconfig -p` — the one and only command this provider
/// ever invokes; never `ldconfig` without `-p` (which can rebuild/modify the cache). If
/// `ldconfig` is unavailable or its output is unparseable, this provider simply returns an empty
/// cache — it never fails the scan. See skill.md (Phase 6F) §12.
/// </summary>
public sealed class LdconfigProvider(IProcessRunner processRunner) : ILdconfigProvider
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    // Real `ldconfig -p` output looks like:
    //     1000 libs found in cache `/etc/ld.so.cache'
    //     	libz.so.1 (libc6,x86-64) => /lib/x86_64-linux-gnu/libz.so.1
    private static readonly Regex CacheLine = new(@"^\s*(?<name>\S+)\s*\([^)]*\)\s*=>\s*(?<path>\S+)\s*$", RegexOptions.Compiled);

    public async Task<IReadOnlyDictionary<string, string>> GetCacheAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRequest { Executable = "ldconfig", Arguments = ["-p"], Timeout = CommandTimeout },
            cancellationToken);

        if (!result.Success)
        {
            return new Dictionary<string, string>(); // unavailable — this tier simply contributes nothing
        }

        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in result.StandardOutput.Split('\n'))
        {
            var match = CacheLine.Match(line);
            if (!match.Success)
            {
                continue; // header line or unrecognized shape — skipped, never guessed at
            }

            // First entry for a given name wins — ldconfig itself lists preferred candidates
            // first for a given architecture/ABI.
            cache.TryAdd(match.Groups["name"].Value, match.Groups["path"].Value);
        }

        return cache;
    }
}
