using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Networking;
using ServerSleuth.Windows.Common;

namespace ServerSleuth.Windows.Networking;

/// <summary>
/// Turns IPortInspector's NetworkEndpoint results into Port entities. The only correlation
/// performed here is what the endpoint itself already carries (owning PID) — building an
/// actual Port-DEPENDS_ON-Process graph edge is Phase 5's job (Correlation), not this
/// scanner's. See skill.md §33.
/// </summary>
public sealed class WindowsPortScanner(IPortInspector portInspector, ILogger<WindowsPortScanner> logger) : IDiscoveryScanner
{
    public string Id => "windows-port-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

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

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} endpoints", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        return DiscoveryResult.Success(Id, entities);
    }

    internal static Port BuildEntity(NetworkEndpoint endpoint, int index)
    {
        var entity = new Port
        {
            Id = $"port:{endpoint.Protocol}:{endpoint.LocalAddress}:{endpoint.LocalPort}:{index}",
            Name = $"{endpoint.Protocol} {endpoint.LocalAddress}:{endpoint.LocalPort}",
            Type = "Port",
            Source = EvidenceSources.WindowsManagementInstrumentation,
            Status = EntityStatus.Listening,
            Confidence = Confidence.VeryHigh(),
            Protocol = endpoint.Protocol,
            LocalAddress = endpoint.LocalAddress,
            Number = endpoint.LocalPort,
            OwningPid = endpoint.ProcessId
        };

        entity.AddEvidence(new EvidenceRecord
        {
            Type = EvidenceType.NetworkSocket,
            Location = $"{endpoint.Protocol} {endpoint.LocalAddress}:{endpoint.LocalPort}",
            Detail = endpoint.ProcessId.HasValue ? $"OwningProcess PID {endpoint.ProcessId}" : null
        });

        if (endpoint.ProcessName is not null)
        {
            entity.SetMetadata("ProcessName", endpoint.ProcessName);
        }
        else if (endpoint.ProcessId is not null)
        {
            entity.SetMetadata("ProcessNameStatus", "Unavailable");
        }

        return entity;
    }
}
