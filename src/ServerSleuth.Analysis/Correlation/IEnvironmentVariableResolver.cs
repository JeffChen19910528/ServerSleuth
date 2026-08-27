namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// Resolves an environment-variable name to its value for path normalization. Injectable so
/// correlation tests are deterministic (never depend on the actual machine's environment) —
/// see skill.md §18. The default implementation reads the current process environment; it
/// never executes a command to obtain a value.
/// </summary>
public interface IEnvironmentVariableResolver
{
    string? GetValue(string name);
}

/// <summary>Reads real environment variables via <see cref="Environment.GetEnvironmentVariable"/>
/// — a lookup, not command execution, so it does not violate the no-execution constraint.</summary>
public sealed class EnvironmentVariableResolver : IEnvironmentVariableResolver
{
    public static readonly EnvironmentVariableResolver Instance = new();

    public string? GetValue(string name) => Environment.GetEnvironmentVariable(name);
}
