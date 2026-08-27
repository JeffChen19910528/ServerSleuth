namespace ServerSleuth.Infrastructure.Runtimes;

/// <summary>Shared configuration so no detector hard-codes its own timeout — see skill.md §23.</summary>
public static class RuntimeDetectionDefaults
{
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);
}
