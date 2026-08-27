namespace ServerSleuth.Windows.IIS;

public sealed record IisApplicationRow
{
    /// <summary>e.g. "/" for the site's root application, "/api" for a sub-application.</summary>
    public required string VirtualPath { get; init; }

    public string? PhysicalPath { get; init; }
    public bool PhysicalPathInherited { get; init; }
    public string? ApplicationPoolName { get; init; }
}
