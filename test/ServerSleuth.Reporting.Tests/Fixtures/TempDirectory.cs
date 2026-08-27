namespace ServerSleuth.Reporting.Tests.Fixtures;

/// <summary>A unique, auto-cleaned scratch directory for export tests — never touches anything
/// outside its own randomly-named subdirectory under the OS temp path.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ServerSleuthReportingTests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
