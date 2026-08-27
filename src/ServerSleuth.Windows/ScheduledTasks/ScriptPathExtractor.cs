using System.Text.RegularExpressions;

namespace ServerSleuth.Windows.ScheduledTasks;

/// <summary>
/// Best-effort extraction of a script file path from a script-host action's arguments (e.g.
/// powershell.exe/cscript.exe invoking a .ps1/.vbs/.js file). See skill.md §7 — record the
/// script path, never execute or dump its contents.
/// </summary>
public static class ScriptPathExtractor
{
    private static readonly Regex ScriptFilePattern = new(
        @"[""']?(?<path>[^""'\s]+\.(?:ps1|vbs|js|bat|cmd))[""']?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> ScriptHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell.exe", "powershell", "pwsh.exe", "pwsh",
        "cscript.exe", "cscript", "wscript.exe", "wscript",
        "cmd.exe", "cmd"
    };

    public static bool IsScriptHost(string? executablePath)
    {
        if (executablePath is null)
        {
            return false;
        }

        var fileName = System.IO.Path.GetFileName(executablePath);
        return ScriptHosts.Contains(fileName);
    }

    public static string? TryExtract(string? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        var match = ScriptFilePattern.Match(arguments);
        return match.Success ? match.Groups["path"].Value : null;
    }
}
