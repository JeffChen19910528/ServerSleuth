using System.IO;
using Microsoft.Extensions.Logging;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

/// <summary>Before <see cref="FileLoggerProvider"/> existed, <see cref="GuiExceptionHandler"/>'s
/// <c>ILogger.LogError</c> call went into a container built with a bare <c>AddLogging()</c> —
/// zero providers registered, so every logged exception was silently discarded and "See
/// application logs for details" was never actually true. These tests exist to keep that
/// regression from coming back unnoticed.</summary>
public class FileLoggerProviderTests
{
    private static string TempLogPath() => Path.Combine(Path.GetTempPath(), $"serversleuth-test-{Guid.NewGuid():N}.log");

    [Fact]
    public void LogError_WithAnException_WritesTheExceptionToTheFile()
    {
        var path = TempLogPath();
        try
        {
            using var provider = new FileLoggerProvider(path);
            var logger = provider.CreateLogger("Test.Category");

            logger.LogError(new InvalidOperationException("boom"), "Something failed.");

            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("Something failed.", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("boom", content);
            Assert.Contains("Test.Category", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_DoesNotTouchTheFilesystem_UntilTheFirstActualLogWrite()
    {
        // GUI-1's composition root must remain side-effect-free to construct — this provider is
        // registered on every CompositionRoot.Build() call, including ones that never log. Use a
        // subdirectory that does not already exist (unlike the bare temp folder itself) so the
        // assertion actually proves the constructor created nothing.
        var directory = Path.Combine(Path.GetTempPath(), $"serversleuth-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "gui.log");
        _ = new FileLoggerProvider(path);

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void IsEnabled_ForInformationAndAbove_ButNotForDebugOrTrace()
    {
        using var provider = new FileLoggerProvider(TempLogPath());
        var logger = provider.CreateLogger("Test.Category");

        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));
    }
}
