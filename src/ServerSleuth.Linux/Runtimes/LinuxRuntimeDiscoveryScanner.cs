using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Linux.Common;

namespace ServerSleuth.Linux.Runtimes;

/// <summary>
/// Orchestrates every registered Linux IRuntimeDetector independently — one detector failing
/// or finding nothing never affects another's result. Mirrors
/// `ServerSleuth.Windows.Runtimes.RuntimeDiscoveryScanner` exactly, sharing the same
/// platform-neutral `IRuntimeDetector`/`RuntimeEntityBuilder` (Infrastructure).
/// </summary>
public sealed class LinuxRuntimeDiscoveryScanner(IEnumerable<IRuntimeDetector> detectors, ILogger<LinuxRuntimeDiscoveryScanner> logger)
    : IDiscoveryScanner
{
    public string Id => "linux-runtime-discovery-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        var entities = new List<DiscoveryEntity>();
        var errors = new List<DiscoveryError>();
        var anyFailed = false;
        var anyPartial = false;

        foreach (var detector in detectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RuntimeDetectionResult result;
            try
            {
                result = await detector.DetectAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Detector {DetectorId} threw unexpectedly", detector.Id);
                result = RuntimeDetectionResult.Failure(ex.Message);
            }

            switch (result.Status)
            {
                case ScannerStatus.Failed:
                    anyFailed = true;
                    errors.Add(new DiscoveryError { ScannerId = detector.Id, Message = result.ErrorMessage ?? "Detector failed." });
                    break;
                case ScannerStatus.PartiallySupported:
                    anyPartial = true;
                    if (result.ErrorMessage is not null)
                    {
                        errors.Add(new DiscoveryError { ScannerId = detector.Id, Message = result.ErrorMessage, IsPermissionFailure = true });
                    }
                    break;
                case ScannerStatus.NotInstalled:
                    break; // Not detected is a normal outcome, not an error.
            }

            entities.AddRange(result.Rows.Select(row => RuntimeEntityBuilder.Build(row, ResolveSource)));
        }

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} runtime/SDK entities", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = anyFailed || anyPartial ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = errors };
    }

    private static string ResolveSource(RuntimeDetectionRow row) => EvidenceSources.Command;
}
