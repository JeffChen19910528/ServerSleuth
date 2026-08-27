using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using CoreRuntime = ServerSleuth.Core.Models.Runtime;
using CoreSdk = ServerSleuth.Core.Models.Sdk;

namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>
/// One generic mapper shared by every detector, on every platform — not one per runtime
/// family, and not one per OS — so entity shape/evidence/status rules are defined exactly
/// once. See skill.md §2/§15. Platform-neutral: it knows nothing about Windows registry keys
/// or Linux PATH conventions, only the already-normalized <see cref="RuntimeDetectionRow"/>
/// shape every detector (Windows or Linux) produces.
/// </summary>
public static class RuntimeEntityBuilder
{
    /// <summary>
    /// Maps a detection row to a domain entity. <paramref name="resolveSource"/> lets each
    /// platform's orchestrating scanner supply its own human-readable Source label (e.g.
    /// "Windows Registry" vs "Command") without this shared builder needing to know about any
    /// platform-specific evidence-source vocabulary; omitting it falls back to a generic label.
    /// </summary>
    public static DiscoveryEntity Build(RuntimeDetectionRow row, Func<RuntimeDetectionRow, string>? resolveSource = null)
    {
        var path = row.InstallationPath ?? row.ExecutablePath ?? "unknown-path";
        var id = $"runtime:{row.Family}:{row.Name}:{row.Version ?? "unknown-version"}:{path}";
        var confidence = DetermineConfidence(row);
        var source = resolveSource?.Invoke(row) ?? DefaultSource(row);

        DiscoveryEntity entity = row.EntityKind == RuntimeEntityKind.Sdk
            ? new CoreSdk
            {
                Id = id,
                Name = row.Name,
                Type = row.Family,
                Source = source,
                Status = EntityStatus.Installed,
                Confidence = confidence,
                Version = row.Version,
                Architecture = row.Architecture,
                Path = row.ExecutablePath ?? row.InstallationPath,
                DetectionCommand = row.Command
            }
            : new CoreRuntime
            {
                Id = id,
                Name = row.Name,
                Type = row.Family,
                Source = source,
                Status = EntityStatus.Installed,
                Confidence = confidence,
                Version = row.Version,
                Architecture = row.Architecture,
                Path = row.ExecutablePath ?? row.InstallationPath,
                DetectionCommand = row.Command
            };

        foreach (var detectionSource in row.DetectionSources)
        {
            entity.AddEvidence(detectionSource switch
            {
                "Registry" => new EvidenceRecord { Type = EvidenceType.Registry, Location = row.RegistryPath ?? "unknown" },
                "Command" => new EvidenceRecord { Type = EvidenceType.Command, Location = row.Command ?? "unknown" },
                "KnownPath" => new EvidenceRecord { Type = EvidenceType.FileSystem, Location = path },
                _ => new EvidenceRecord { Type = EvidenceType.Command, Location = detectionSource }
            });
        }

        entity.SetMetadata("Family", row.Family);
        entity.SetMetadata("ExecutableAvailable", row.ExecutableAvailable.ToString());
        entity.SetMetadata("DetectionSources", string.Join(",", row.DetectionSources));

        if (row.Edition is not null) entity.SetMetadata("Edition", row.Edition);
        if (row.ConflictNote is not null) entity.SetMetadata("ConflictNote", row.ConflictNote);
        if (row.ExecutablePath is not null) entity.SetMetadata("ExecutablePath", row.ExecutablePath);
        if (row.InstallationPath is not null) entity.SetMetadata("InstallationPath", row.InstallationPath);

        foreach (var (name, value) in row.EnvironmentVariables)
        {
            entity.SetMetadata($"Env.{name}", value);
        }

        return entity;
    }

    private static string DefaultSource(RuntimeDetectionRow row) =>
        row.DetectionSources.Contains("Registry") ? "Registry" : "Command";

    private static Confidence DetermineConfidence(RuntimeDetectionRow row)
    {
        if (row.ExecutableAvailable && row.DetectionSources.Contains("Command"))
        {
            return Confidence.VeryHigh();
        }

        if (row.DetectionSources.Contains("Registry"))
        {
            return Confidence.High();
        }

        return Confidence.Medium();
    }
}
