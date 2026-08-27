namespace ServerSleuth.Cli.Output;

/// <summary>Writes to two injected <see cref="TextWriter"/>s — <see cref="Console.Out"/>/
/// <see cref="Console.Error"/> in production, an in-memory <see cref="StringWriter"/> in tests.</summary>
public sealed class TextWriterConsoleWriter(TextWriter output, TextWriter error) : IConsoleWriter
{
    public void WriteLine(string line) => output.WriteLine(line);
    public void WriteErrorLine(string line) => error.WriteLine(line);
}
