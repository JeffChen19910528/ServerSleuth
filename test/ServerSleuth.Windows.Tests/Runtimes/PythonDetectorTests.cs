using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Runtimes;

public class PythonDetectorTests
{
    private const string PyPath = @"C:\Windows\py.exe";
    private const string Python312 = @"C:\Users\dev\AppData\Local\Programs\Python\Python312\python.exe";
    private const string Python311 = @"C:\Users\dev\AppData\Local\Programs\Python\Python311\python.exe";

    [Fact]
    public async Task DetectAsync_PyLauncherListsMultipleVersions_EachBecomesOwnRow()
    {
        var launcherOutput = $" -V:3.12 *        {Python312}\n -V:3.11          {Python311}";

        var locator = new FakeExecutableLocator(new() { ["py.exe"] = PyPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{PyPath}|-0p"] = ProcessResult.Ok(0, launcherOutput, string.Empty, TimeSpan.Zero),
            [$"{Python312}|--version"] = ProcessResult.Ok(0, "Python 3.12.1", string.Empty, TimeSpan.Zero),
            [$"{Python311}|--version"] = ProcessResult.Ok(0, "Python 3.11.9", string.Empty, TimeSpan.Zero)
        });
        var fileSystem = new FakeFileSystemReader();

        var detector = new PythonDetector(locator, runner, fileSystem);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.Version == "3.12.1");
        Assert.Contains(result.Rows, r => r.Version == "3.11.9");
    }

    [Fact]
    public async Task DetectAsync_KnownDirectoryScan_FindsInterpreterWithoutPyLauncher()
    {
        var locator = new FakeExecutableLocator(new()); // no py.exe, no python.exe on PATH
        var fileSystem = new FakeFileSystemReader();
        var programFiles = @"C:\Program Files";
        fileSystem.SetSubdirectories(programFiles, @"C:\Program Files\Python312");
        fileSystem.AddExisting(@"C:\Program Files\Python312\python.exe");

        var runner = new FakeProcessRunner(new()
        {
            [@"C:\Program Files\Python312\python.exe|--version"] = ProcessResult.Ok(0, "Python 3.12.1", string.Empty, TimeSpan.Zero)
        });

        var detector = new PythonDetector(locator, runner, fileSystem);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Single(result.Rows);
        Assert.Equal("3.12.1", result.Rows[0].Version);
    }

    [Fact]
    public async Task DetectAsync_NoInterpreterFoundAnywhere_ReturnsNotDetected()
    {
        var detector = new PythonDetector(new FakeExecutableLocator(new()), new FakeProcessRunner(new()), new FakeFileSystemReader());
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }

    [Fact]
    public async Task DetectAsync_ResolvedPathFailsToExecute_IsExcludedNotCrashed()
    {
        var locator = new FakeExecutableLocator(new() { ["python.exe"] = @"C:\broken\python.exe" });
        var detector = new PythonDetector(locator, new FakeProcessRunner(new()), new FakeFileSystemReader());

        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }
}
