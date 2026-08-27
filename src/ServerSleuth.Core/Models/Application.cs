namespace ServerSleuth.Core.Models;

/// <summary>
/// A logical application grouped from lower-level discoveries (IIS site + app pool + DLLs +
/// runtime, or Windows service + executable + port) — see skill.md §20. Grouping confidence
/// reflects how strong the correlation evidence was, never an invented name.
/// </summary>
public sealed class Application : DiscoveryEntity
{
    public IReadOnlyList<string> ComponentEntityIds { get; init; } = [];
}
