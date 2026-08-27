namespace ServerSleuth.Windows.Binaries;

/// <summary>
/// Static PE header analysis — never loads, executes, or activates the binary. Backed by
/// System.Reflection.Metadata's PEReader (an official, first-party, read-only PE/CLR-metadata
/// reader — preferred here over a hand-rolled byte-offset parser per skill.md §33's "prefer
/// BCL/manual minimal parser" guidance; this is about as minimal as a correct implementation
/// gets without reimplementing the PE spec by hand).
/// </summary>
public interface IPeAnalyzer
{
    PeAnalysisResult Analyze(string filePath);
}
