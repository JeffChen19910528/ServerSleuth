using System.IO;
using Microsoft.Extensions.Logging;

namespace ServerSleuth.Gui.Services;

/// <summary>The GUI's only actual log sink. Before this existed, <see cref="GuiExceptionHandler"/>
/// called <c>ILogger.LogError</c> into a container built with a bare <c>services.AddLogging()</c>
/// — which registers no provider at all, so every logged exception was silently discarded and the
/// user-facing "See application logs for details" message was, in practice, never true. This
/// writes one plain-text line per log entry to a single rolling-by-day file under
/// <c>%LOCALAPPDATA%\ServerSleuth\logs\</c> — never <see cref="Console"/> (a WinExe has no console
/// to receive it) and never anywhere under the install/publish directory (which a single-file
/// self-contained deployment may not have write access to, and which is not a sensible place for
/// per-user runtime data). No third-party logging package — <c>Microsoft.Extensions.Logging</c>
/// is already referenced; this only implements its two smallest extension points.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly object _writeLock = new();

    public FileLoggerProvider()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ServerSleuth", "logs", $"gui-{DateTime.Now:yyyy-MM-dd}.log"))
    {
    }

    /// <summary>Exposed for tests — production code always uses the parameterless constructor's
    /// real per-user path. Deliberately does NOT touch the filesystem here (no directory/file
    /// creation) — GUI-1's composition root must stay side-effect-free to construct
    /// (<see cref="Composition.CompositionRoot.Build"/> registers this provider on every
    /// resolution, including in tests that never log anything); the log directory is created
    /// lazily, only on the first actual write.</summary>
    public FileLoggerProvider(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public string LogFilePath => _logFilePath;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void WriteLine(string line)
    {
        // A logging failure (disk full, permissions, path unavailable) must never itself become
        // an unhandled exception — this is the last-resort diagnostic path, not a critical one.
        try
        {
            lock (_writeLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Deliberately swallowed — see the comment above.
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            provider.WriteLine(line);
        }
    }
}
