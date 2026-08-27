namespace ServerSleuth.Windows.Binaries;

public sealed record DirectoryWalkResult
{
    public IReadOnlyList<string> Files { get; init; } = [];
    public bool DepthLimitReached { get; init; }
    public bool FileLimitReached { get; init; }
    public int ReparsePointsSkipped { get; init; }
    public IReadOnlyList<string> AccessDeniedDirectories { get; init; } = [];
}
