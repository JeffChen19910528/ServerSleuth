using System.Diagnostics;
using System.IO;

namespace ServerSleuth.Gui.Tests.Views;

/// <summary>Guards a real, previously-shipped defect that an in-process xUnit test structurally
/// cannot detect: with <c>InvariantGlobalization</c> enabled (see <c>ServerSleuth.Gui.csproj</c>),
/// WPF's binding engine threw <c>InvalidOperationException: Cannot find non-neutral culture
/// related to 'en-us'</c> the first time any bound control on any real page attached to the
/// visual tree — surfaced to an actual user only as the generic "unexpected error" footer message,
/// after Dashboard -> switch to Traditional Chinese -> Scan. Fixed in <c>App</c>'s static
/// constructor (see its own doc comment). The VSTest host process this class runs in does NOT
/// itself set <c>InvariantGlobalization</c>, so <see cref="RealWindowNavigationSmokeTests"/> alone
/// cannot reproduce the failure regardless of whether the fix is present — this test instead runs
/// <c>ServerSleuth.Gui.RealWindowHarness</c> (a standalone executable that DOES set
/// <c>InvariantGlobalization=true</c>, mirroring the real shipped <c>ServerSleuth.Gui.exe</c>
/// exactly) as a real child process and asserts on its exit code, so the actual runtime
/// configuration that caused the original defect is genuinely exercised.</summary>
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
