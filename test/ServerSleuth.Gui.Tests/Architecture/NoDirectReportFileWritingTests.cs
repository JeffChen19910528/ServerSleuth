using System.IO;

namespace ServerSleuth.Gui.Tests.Architecture;

/// <summary>
/// GUI-7B §8/§13/§25: "DO NOT use File.WriteAllText/File.WriteAllBytes or any other direct raw
/// report-writing path" — <c>ServerSleuth.Gui</c> reaches the real exporter ONLY through
/// <see cref="ServerSleuth.Gui.Services.IGuiReportExportService"/> (see that interface's own doc
/// comment for why <c>ServerSleuth.Gui.ExecutionHost</c> is the one place allowed to reference
/// <c>ServerSleuth.Reporting</c> at all). A source-text sweep, not a reflection check, because
/// <c>File.WriteAllText</c>/<c>File.WriteAllBytes</c> are static BCL calls with no distinct type
/// to reflect on.
/// </summary>
public class NoDirectReportFileWritingTests
{
    private static readonly string[] ForbiddenSubstrings = ["File.WriteAllText", "File.WriteAllBytes", "StreamWriter", "FileStream"];

    [Fact]
    public void GuiProjectSource_ContainsNoDirectRawFileWritingCall()
    {
        var guiSourceDir = FindProjectDirectory("ServerSleuth.Gui");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(guiSourceDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            // Only actual code lines — several doc comments in this codebase explain what is
            // deliberately NOT done by naming these exact APIs defensively (e.g. "never
            // File.WriteAllText here"), which would otherwise false-positive this sweep.
            var codeLines = File.ReadLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
            var content = string.Join('\n', codeLines);

            foreach (var forbidden in ForbiddenSubstrings)
            {
                if (content.Contains(forbidden, StringComparison.Ordinal))
                {
                    offenders.Add($"{file}: {forbidden}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static string FindProjectDirectory(string projectName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", projectName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate the {projectName} source directory from the test output directory.");
    }
}
