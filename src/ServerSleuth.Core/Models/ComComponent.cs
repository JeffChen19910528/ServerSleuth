namespace ServerSleuth.Core.Models;

/// <summary>COM/ActiveX registration — see skill.md §9. Distinguishes Registered from
/// Observed-in-use via Status; never assume the latter from the former alone.</summary>
public sealed class ComComponent : DiscoveryEntity
{
    public required string Clsid { get; init; }
    public string? ProgId { get; init; }
    public string? InprocServer32 { get; init; }
    public string? LocalServer32 { get; init; }
    public string? TypeLibrary { get; init; }
    public string? ThreadingModel { get; init; }
}
