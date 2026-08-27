namespace ServerSleuth.Core.Models;

/// <summary>Captures a variable's name and scope only. If the value looks like a secret,
/// it must never be stored here — see skill.md §24.</summary>
public sealed class EnvironmentVariable : DiscoveryEntity
{
    public string? Scope { get; init; } // "Process", "User", "Machine", "Container"
    public string? RedactedValuePreview { get; init; } // e.g. "[REDACTED]" or a safe non-secret value
    public bool SecretDetected { get; init; }
}
