using System.Diagnostics;
using System.IO;

namespace ServerSleuth.Gui.Tests.Views;

/// <summary>Guards a real, previously-shipped defect that an in-process xUnit test structurally
/// cannot detect: WPF's binding engine can throw <c>InvalidOperationException: Cannot find
/// non-neutral culture related to 'en-us'</c> (from <c>BindingExpression.GetCulture()</c> →
/// <c>XmlLanguage.GetSpecificCulture()</c>) the first time a numeric binding on a real page
/// attaches to a live visual tree — surfaced to an actual user only as the generic "unexpected
/// error" footer message. This was originally caused by an <c>App</c> static constructor that
/// pinned every <c>FrameworkElement.LanguageProperty</c> to
/// <c>CultureInfo.InvariantCulture</c>'s (empty) IETF tag as a workaround for a NOW-REMOVED
/// <c>InvariantGlobalization=true</c> (see the git history around commit 6929da3, which dropped
/// <c>InvariantGlobalization</c> for an unrelated input-language freeze but left this override in
/// place) — that override itself turned out to be unsafe even without
/// <c>InvariantGlobalization</c> and was deleted outright, restoring WPF's own default
/// <c>FrameworkElement.Language</c> resolution. The VSTest host process this class runs in never
/// constructs a real <see cref="System.Windows.Application"/>, so
/// <see cref="RealWindowNavigationSmokeTests"/> alone cannot reproduce a layout-attach-time
/// binding failure regardless of whether the fix is present — this test instead runs
/// <c>ServerSleuth.Gui.RealWindowHarness</c> (a standalone executable, real
/// <see cref="System.Windows.Application"/>/<c>MainWindow</c>/layout pass included) as a real
/// child process and asserts on its exit code.</summary>
public class RealWindowRuntimeConfigTests
{
    private static string HarnessExecutablePath()
    {
        // test\ServerSleuth.Gui.Tests\bin\{Configuration}\net8.0-windows -> ...\ServerSleuth.Gui.RealWindowHarness\bin\{Configuration}\net8.0-windows\ServerSleuth.Gui.RealWindowHarness.exe
        var testOutputDir = AppContext.BaseDirectory;
        var configuration = new DirectoryInfo(testOutputDir).Parent!.Name; // Debug or Release
        var repoTestDir = new DirectoryInfo(testOutputDir).Parent!.Parent!.Parent!.Parent!.FullName; // .../test
        var harnessPath = Path.Combine(repoTestDir, "ServerSleuth.Gui.RealWindowHarness", "bin", configuration, "net8.0-windows", "ServerSleuth.Gui.RealWindowHarness.exe");
        return harnessPath;
    }

    [Fact]
    public void RealWindowHarness_UnderTheRealInvariantGlobalizationRuntimeConfig_ExitsZero()
    {
        var harnessPath = HarnessExecutablePath();
        Assert.True(File.Exists(harnessPath),
            $"Harness executable not found at '{harnessPath}' — build ServerSleuth.Gui.RealWindowHarness before running this test.");

        var startInfo = new ProcessStartInfo(harnessPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(TimeSpan.FromSeconds(30));

        Assert.True(exited, "Harness process did not exit within 30 seconds.");
        Assert.True(process.ExitCode == 0,
            $"Harness exited with code {process.ExitCode} — a real WPF window/layout regression occurred.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }
}
