using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Linux.Common;

namespace ServerSleuth.Linux.Networking;

/// <summary>
/// Turns `IPortInspector`'s `NetworkEndpoint` results into `Port` entities — mirrors
/// `WindowsPortScanner`'s structure exactly, since the cross-platform `IPortInspector`
/// abstraction (Phase 2) is what makes this pairing possible without duplicating logic.
/// </summary>
public sealed class LinuxPortScanner(IPortInspector portInspector, ILogger<LinuxPortScanner> logger) : IDiscoveryScanner
{
    public string Id => "linux-port-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        IReadOnlyList<NetworkEndpoint> endpoints;
        try
        {
            endpoints = await portInspector.GetListeningEndpointsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ScannerLogEvents.ScannerFailed, ex, "{ScannerId} failed to enumerate endpoints", Id);
            return DiscoveryResult.Failure(Id, new DiscoveryError { ScannerId = Id, Message = ex.Message, Exception = ex.GetType().Name });
        }

        var entities = endpoints.Select((endpoint, index) => BuildEntity(endpoint, index)).ToList();
        var unresolvedOwnership = endpoints.Count(e => e.ProcessId is null);

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} endpoints", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = unresolvedOwnership > 0 ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;
        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities };
    }

    internal static Port BuildEntity(NetworkEndpoint endpoint, int index)
    {
        var entity = new Port
        {
            Id = $"port:{endpoint.Protocol}:{endpoint.LocalAddress}:{endpoint.LocalPort}:{index}",
            Name = $"{endpoint.Protocol} {endpoint.LocalAddress}:{endpoint.LocalPort}",
            Type = "Port",
            Source = EvidenceSources.ProcNet,
            Status = EntityStatus.Listening,
            Confidence = endpoint.ProcessId is not null ? Confidence.VeryHigh() : Confidence.High(),
            Protocol = endpoint.Protocol,
            LocalAddress = endpoint.LocalAddress,
            Number = endpoint.LocalPort,
            OwningPid = endpoint.ProcessId
        };

        entity.AddEvidence(new EvidenceRecord
        {
            Type = EvidenceType.NetworkSocket,
            Location = $"{EvidenceSources.ProcNet}/{endpoint.Protocol.ToLowerInvariant()}",
            Detail = endpoint.ProcessId.HasValue ? $"Owning PID {endpoint.ProcessId} (via socket inode)" : null
        });

        if (endpoint.ProcessId is null)
        {
            entity.SetMetadata("OwningPidStatus", "Unresolved");
        }

        return entity;
    }
}
