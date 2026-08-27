namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// Result of normalizing a raw Windows path for identity resolution — see skill.md §5-6.
/// </summary>
public sealed record NormalizedPath
{
    /// <summary>The path exactly as it was supplied, before any normalization.</summary>
    public required string OriginalPath { get; init; }

    /// <summary>Trimmed/unquoted/separator-normalized/environment-resolved (where safely
    /// resolvable) form, preserving original casing for display.</summary>
    public required string Value { get; init; }

    /// <summary>Case-folded form of <see cref="Value"/> used as a dictionary/lookup key.
    /// Windows paths are case-insensitive, so this — not <see cref="Value"/> — is what
    /// correlation rules must compare on.</summary>
    public required string ComparisonKey { get; init; }

    public bool IsUnc { get; init; }

    /// <summary>True if the raw path contained an environment-variable reference that could
    /// not be resolved — the unresolved reference is preserved in <see cref="Value"/> rather
    /// than guessed at. See skill.md §6.</summary>
    public bool EnvironmentVariableUnresolved { get; init; }
}
