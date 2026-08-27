using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Targets;

namespace ServerSleuth.Core.Interfaces;

/// <summary>
/// Immutable context passed to every scanner run. Carries no mutable global state —
/// a scanner must not rely on anything beyond what's here. See skill.md §5, §27.
/// </summary>
public sealed class DiscoveryContext
{
    public required ScanProfile Profile { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>Optional scan roots to limit filesystem-based scanners — see skill.md §36
    /// ("do not scan the entire filesystem indiscriminately").</summary>
    public IReadOnlyList<string> ScanRoots { get; init; } = [];

    /// <summary>What is being scanned — see skill.md (Phase 10C) §4. Optional (not `required`)
    /// so every existing call site across the codebase keeps compiling unchanged; defaults to
    /// the one local target with an unresolved platform. No scanner reads this today — platform
    /// selection remains entirely the composition root's job (skill.md §10), never a branch
    /// inside <see cref="Orchestration.DiscoveryEngine"/> or any <see cref="IDiscoveryScanner"/>.
    /// This property exists purely so a target's identity can be threaded through discovery for
    /// future use (e.g. CLI display), without requiring a second, parallel context type.</summary>
    public ScanTarget Target { get; init; } = ScanTarget.Local();
}
