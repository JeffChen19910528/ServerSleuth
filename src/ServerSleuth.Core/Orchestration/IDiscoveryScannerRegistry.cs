using ServerSleuth.Core.Interfaces;

namespace ServerSleuth.Core.Orchestration;

/// <summary>
/// A deterministic, in-process catalog of every registered <see cref="IDiscoveryScanner"/> —
/// see skill.md (Phase 6G) §3. Availability is determined entirely by which platform's
/// registration method (`AddServerSleuthWindows()`/`AddServerSleuthLinux()`) the composition
/// root calls, never by an `if (OperatingSystem.IsWindows())`-style check inside this registry
/// or the engine that consumes it. Not a plugin marketplace — no dynamic assembly loading, no
/// runtime discovery of scanner types; every scanner is registered explicitly and known at
/// composition time.
/// </summary>
public interface IDiscoveryScannerRegistry
{
    /// <summary>Every registered scanner, in a fixed, deterministic order (by <see cref="IDiscoveryScanner.Id"/>,
    /// ordinal) — never DI-container registration order or dictionary enumeration order, either
    /// of which can vary between runs/frameworks.</summary>
    IReadOnlyList<IDiscoveryScanner> Scanners { get; }
}
