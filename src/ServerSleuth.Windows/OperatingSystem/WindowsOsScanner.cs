using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Registry;
using CoreOperatingSystem = ServerSleuth.Core.Models.OperatingSystem;

namespace ServerSleuth.Windows.OperatingSystem;

/// <summary>
/// Discovers the current machine's identity and OS details. Only fields that were actually
/// obtained are populated — nothing here is guessed to fill a field. See skill.md §3-4.
/// </summary>
public sealed class WindowsOsScanner(IWindowsRegistryReader registryReader, ILogger<WindowsOsScanner> logger)
    : IDiscoveryScanner
{
    private const string CurrentVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public string Id => "windows-os-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Windows;

    public Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(Infrastructure.Common.ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = EnvironmentSnapshot.Capture();
        var registryValues = registryReader.GetValues(RegistryHive.LocalMachine, RegistryView.Registry64, CurrentVersionKeyPath);

        if (!registryValues.Success)
        {
            logger.LogWarning(Infrastructure.Common.ScannerLogEvents.PermissionDenied,
                "{ScannerId} could not read {KeyPath}: {Status}", Id, CurrentVersionKeyPath, registryValues.Status);
        }

        var entities = BuildEntities(snapshot, registryValues.Value);

        logger.LogInformation(Infrastructure.Common.ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} entities", Id, entities.Count);
        logger.LogInformation(Infrastructure.Common.ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = registryValues.Success ? ScannerStatus.Supported : ScannerStatus.PartiallySupported;
        return Task.FromResult(new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities });
    }

    /// <summary>
    /// Pure mapping from already-captured inputs to domain entities — kept separate from
    /// ScanAsync so it is unit-testable without touching the real registry/environment.
    /// </summary>
    internal static IReadOnlyList<DiscoveryEntity> BuildEntities(
        EnvironmentSnapshot snapshot,
        IReadOnlyDictionary<string, object?>? registryValues)
    {
        var server = new Server
        {
            Id = $"server:{snapshot.MachineName}",
            Name = snapshot.MachineName,
            Type = "Server",
            Source = EvidenceSources.WindowsEnvironment,
            Status = EntityStatus.Running,
            Confidence = Confidence.VeryHigh(),
            Hostname = snapshot.MachineName,
            Domain = snapshot.UserDomainName
        };
        server.AddEvidence(new EvidenceRecord { Type = EvidenceType.Command, Location = "Environment.MachineName" });

        var productName = registryValues?.GetValueOrDefault("ProductName") as string;

        var os = new CoreOperatingSystem
        {
            Id = $"os:{snapshot.MachineName}",
            Name = productName ?? snapshot.OsDescription,
            Type = "OperatingSystem",
            Source = productName is not null ? EvidenceSources.WindowsRegistry : EvidenceSources.WindowsEnvironment,
            Status = EntityStatus.Running,
            Confidence = productName is not null ? Confidence.VeryHigh() : Confidence.High(),
            Architecture = ArchitectureMapper.FromRuntimeArchitecture(snapshot.OsArchitecture),
            Platform = productName ?? snapshot.OsDescription,
            Edition = registryValues?.GetValueOrDefault("EditionID") as string
        };

        os.AddEvidence(new EvidenceRecord { Type = EvidenceType.Command, Location = "RuntimeInformation.OSDescription", Detail = snapshot.OsDescription });

        if (registryValues is not null)
        {
            os.AddEvidence(new EvidenceRecord { Type = EvidenceType.Registry, Location = $@"HKLM\{CurrentVersionKeyPath}" });

            var buildNumber = registryValues.GetValueOrDefault("CurrentBuildNumber") as string;
            var ubr = registryValues.GetValueOrDefault("UBR")?.ToString();
            if (buildNumber is not null)
            {
                os.SetMetadata("BuildNumber", ubr is not null ? $"{buildNumber}.{ubr}" : buildNumber);
            }

            var displayVersion = registryValues.GetValueOrDefault("DisplayVersion") as string
                                  ?? registryValues.GetValueOrDefault("ReleaseId") as string;
            if (displayVersion is not null)
            {
                os.Version = displayVersion;
            }
        }

        os.SetMetadata("SystemDirectory", snapshot.SystemDirectory);
        os.SetMetadata("SystemDrive", Path.GetPathRoot(snapshot.SystemDirectory) ?? string.Empty);
        os.SetMetadata("ExecutionUser", $@"{snapshot.UserDomainName}\{snapshot.UserName}");
        os.SetMetadata("DotNetRuntime", snapshot.FrameworkDescription);

        return [server, os];
    }
}
