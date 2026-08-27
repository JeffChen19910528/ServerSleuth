using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Networking;

namespace ServerSleuth.Linux.Networking;

/// <summary>
/// Linux implementation of the cross-platform `IPortInspector` contract (Phase 2), sourced from
/// `/proc/net/{tcp,tcp6,udp,udp6}` — never `netstat`/`ss`/`lsof` (skill.md (Phase 6A) §5).
/// TCP rows are included only in the LISTEN state; UDP has no listen concept in the kernel's
/// state machine, so (mirroring the Windows implementation's own convention) every UDP row is
/// treated as a bound/"Listening" endpoint.
/// </summary>
public sealed class LinuxPortInspector(IFileSystemReader fileSystemReader, ISocketOwnershipResolver ownershipResolver)
    : IPortInspector
{
    public Task<IReadOnlyList<NetworkEndpoint>> GetListeningEndpointsAsync(CancellationToken cancellationToken)
    {
        var inodeToPid = ownershipResolver.BuildInodeToPidMap();
        var endpoints = new List<NetworkEndpoint>();

        AddFromSource("/proc/net/tcp", "TCP", tcpOnlyListening: true, inodeToPid, endpoints, cancellationToken);
        AddFromSource("/proc/net/tcp6", "TCP", tcpOnlyListening: true, inodeToPid, endpoints, cancellationToken);
        AddFromSource("/proc/net/udp", "UDP", tcpOnlyListening: false, inodeToPid, endpoints, cancellationToken);
        AddFromSource("/proc/net/udp6", "UDP", tcpOnlyListening: false, inodeToPid, endpoints, cancellationToken);

        return Task.FromResult<IReadOnlyList<NetworkEndpoint>>(endpoints);
    }

    private void AddFromSource(
        string path,
        string protocol,
        bool tcpOnlyListening,
        IReadOnlyDictionary<string, int> inodeToPid,
        List<NetworkEndpoint> endpoints,
        CancellationToken cancellationToken)
    {
        var result = fileSystemReader.ReadTextAsync(path, cancellationToken).GetAwaiter().GetResult();
        if (!result.Success)
        {
            return; // e.g. IPv6 disabled -> tcp6/udp6 missing; not an error worth reporting
        }

        foreach (var row in ProcNetParser.Parse(result.Value!))
        {
            if (tcpOnlyListening && !string.Equals(row.StateHex, ProcNetParser.TcpListenState, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            inodeToPid.TryGetValue(row.Inode, out var pid);

            endpoints.Add(new NetworkEndpoint
            {
                Protocol = protocol,
                LocalAddress = row.LocalAddress,
                LocalPort = row.LocalPort,
                ProcessId = pid == 0 ? null : pid,
                State = "Listening"
            });
        }
    }
}
