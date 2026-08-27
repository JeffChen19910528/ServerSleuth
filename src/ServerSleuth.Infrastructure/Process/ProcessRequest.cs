namespace ServerSleuth.Infrastructure.Process;

/// <summary>
/// Describes a command to execute. Executable and Arguments are always kept separate —
/// never build a shell command string from these, to avoid command injection. See skill.md §35.
/// </summary>
public sealed record ProcessRequest
{
    public required string Executable { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>();
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
