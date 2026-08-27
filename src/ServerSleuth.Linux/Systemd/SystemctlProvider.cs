using System.Text.Json.Serialization;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Systemd;

/// <summary>
/// Real systemd reader, backed entirely by `systemctl` invoked via `IProcessRunner` (fixed
/// executable, fixed/discrete arguments, no shell) — never `systemctl start/stop/restart/
/// enable/disable/reload`, and never a unit-file write. See skill.md (Phase 6A) §7, §9-10.
/// </summary>
public sealed class SystemctlProvider(IProcessRunner processRunner) : ISystemdProvider
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    private const string ShowProperties =
        "Description,LoadState,ActiveState,SubState,UnitFileState,ExecStart,User,WorkingDirectory,FragmentPath";

    public SystemdProbeResult GetSnapshot()
    {
        var listResult = processRunner.RunAsync(
            new ProcessRequest
            {
                Executable = "systemctl",
                Arguments = ["list-units", "--type=service", "--all", "--no-legend", "--no-pager", "--output=json"],
                Timeout = CommandTimeout
            },
            CancellationToken.None).GetAwaiter().GetResult();

        if (!listResult.Success)
        {
            var status = listResult.Status == OperationStatus.StartFailed
                ? SystemdAvailability.NotInstalled
                : SystemdAvailability.Failed;

            return new SystemdProbeResult { Status = status, ErrorMessage = listResult.StandardError };
        }

        List<SystemctlUnitJson>? listedUnits;
        try
        {
            listedUnits = System.Text.Json.JsonSerializer.Deserialize<List<SystemctlUnitJson>>(listResult.StandardOutput);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new SystemdProbeResult { Status = SystemdAvailability.Failed, ErrorMessage = $"Could not parse systemctl JSON output: {ex.Message}" };
        }

        if (listedUnits is null)
        {
            return new SystemdProbeResult { Status = SystemdAvailability.Available, Units = [] };
        }

        var units = new List<SystemdUnitRow>();
        var partialFailures = new List<string>();

        foreach (var listed in listedUnits)
        {
            var showResult = processRunner.RunAsync(
                new ProcessRequest
                {
                    Executable = "systemctl",
                    Arguments = ["show", listed.Unit, "--no-pager", $"--property={ShowProperties}"],
                    Timeout = CommandTimeout
                },
                CancellationToken.None).GetAwaiter().GetResult();

            if (!showResult.Success)
            {
                partialFailures.Add($"{listed.Unit}: {showResult.Status}");
                units.Add(new SystemdUnitRow
                {
                    UnitName = listed.Unit,
                    LoadState = listed.Load,
                    ActiveState = listed.Active,
                    SubState = listed.Sub,
                    Description = listed.Description,
                    DetailUnavailable = true
                });
                continue;
            }

            var properties = SystemctlKeyValueParser.Parse(showResult.StandardOutput);

            units.Add(new SystemdUnitRow
            {
                UnitName = listed.Unit,
                Description = properties.GetValueOrDefault("Description") ?? listed.Description,
                LoadState = properties.GetValueOrDefault("LoadState") ?? listed.Load,
                ActiveState = properties.GetValueOrDefault("ActiveState") ?? listed.Active,
                SubState = properties.GetValueOrDefault("SubState") ?? listed.Sub,
                UnitFileState = properties.GetValueOrDefault("UnitFileState"),
                ExecStart = NullIfEmpty(properties.GetValueOrDefault("ExecStart")),
                User = NullIfEmpty(properties.GetValueOrDefault("User")),
                WorkingDirectory = NullIfEmpty(properties.GetValueOrDefault("WorkingDirectory")),
                FragmentPath = NullIfEmpty(properties.GetValueOrDefault("FragmentPath"))
            });
        }

        return new SystemdProbeResult { Status = SystemdAvailability.Available, Units = units, PartialFailures = partialFailures };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private sealed record SystemctlUnitJson
    {
        [JsonPropertyName("unit")] public required string Unit { get; init; }
        [JsonPropertyName("load")] public string? Load { get; init; }
        [JsonPropertyName("active")] public string? Active { get; init; }
        [JsonPropertyName("sub")] public string? Sub { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
    }
}
