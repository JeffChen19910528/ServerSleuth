namespace ServerSleuth.Cli.Output;

/// <summary>Thin abstraction over stdout/stderr so tests can capture CLI output without a real
/// console — see skill.md (Phase 10A) §22 ("use fakes for CLI unit tests").</summary>
public interface IConsoleWriter
{
    void WriteLine(string line);
    void WriteErrorLine(string line);
}
