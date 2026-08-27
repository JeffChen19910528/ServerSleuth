namespace ServerSleuth.Linux.Cron;

/// <summary>
/// Best-effort extraction of an explicit executable path from a cron command — never resolves
/// a bare command name via PATH, never evaluates shell syntax/substitutions. An unresolvable
/// command stays unresolved rather than guessed. See skill.md (Phase 6B) §20-21.
/// </summary>
public static class CronCommandPathExtractor
{
    public static string? TryExtractExecutablePath(string command)
    {
        var trimmed = command.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '/')
        {
            return null; // not an explicit absolute path — never guessed
        }

        var spaceIndex = trimmed.IndexOfAny([' ', '\t']);
        return spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
    }
}
