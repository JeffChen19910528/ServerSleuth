using Microsoft.Win32;
using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.Tests.Fakes;

namespace ServerSleuth.Windows.Tests.Runtimes;

public class JavaDetectorTests
{
    private const string JavaPath = @"C:\Program Files\Eclipse Adoptium\jdk-17\bin\java.exe";
    private const string JavaHome = @"C:\Program Files\Eclipse Adoptium\jdk-17";

    private static readonly string OpenJdkVersionOutput =
        "openjdk version \"17.0.12\" 2024-07-16\nOpenJDK Runtime Environment Temurin-17.0.12+7\nOpenJDK 64-Bit Server VM Temurin-17.0.12+7";

    [Fact]
    public async Task DetectAsync_RegistryOnly_IsReportedWithoutExecutable()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\JavaSoft\JDK", "17.0.12");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\JavaSoft\JDK\17.0.12",
            new Dictionary<string, object?> { ["JavaHome"] = JavaHome });

        var detector = new JavaDetector(reader, new FakeExecutableLocator(new()), new FakeProcessRunner(new()), new FakeFileSystemReader());
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.False(row.ExecutableAvailable);
        Assert.Equal("17.0.12", row.Version);
        Assert.Equal("Java (JDK)", row.Name);
    }

    [Fact]
    public async Task DetectAsync_CommandOnly_DetectsJdkViaJavacPresence()
    {
        var locator = new FakeExecutableLocator(new() { ["java.exe"] = JavaPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{JavaPath}|-version"] = ProcessResult.Ok(0, string.Empty, OpenJdkVersionOutput, TimeSpan.Zero)
        });
        var fileSystem = new FakeFileSystemReader();
        fileSystem.AddExisting(@"C:\Program Files\Eclipse Adoptium\jdk-17\bin\javac.exe");

        var detector = new JavaDetector(new FakeWindowsRegistryReader(), locator, runner, fileSystem);
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Java (JDK)", row.Name);
        Assert.Equal("17.0.12", row.Version);
        Assert.Equal("Eclipse Temurin", row.Edition);
        Assert.True(row.ExecutableAvailable);
    }

    [Fact]
    public async Task DetectAsync_JreWithoutJavac_IsReportedAsJre()
    {
        var locator = new FakeExecutableLocator(new() { ["java.exe"] = JavaPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{JavaPath}|-version"] = ProcessResult.Ok(0, string.Empty, OpenJdkVersionOutput, TimeSpan.Zero)
        });
        var fileSystem = new FakeFileSystemReader(); // no javac.exe

        var detector = new JavaDetector(new FakeWindowsRegistryReader(), locator, runner, fileSystem);
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal("Java (JRE)", Assert.Single(result.Rows).Name);
    }

    [Fact]
    public async Task DetectAsync_RegistryAndCommandAgreeOnPath_MergeIntoOneRowNoConflict()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\JavaSoft\JDK", "17.0.12");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\JavaSoft\JDK\17.0.12",
            new Dictionary<string, object?> { ["JavaHome"] = JavaHome });

        var locator = new FakeExecutableLocator(new() { ["java.exe"] = JavaPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{JavaPath}|-version"] = ProcessResult.Ok(0, string.Empty, OpenJdkVersionOutput, TimeSpan.Zero)
        });
        var fileSystem = new FakeFileSystemReader();
        fileSystem.AddExisting(@"C:\Program Files\Eclipse Adoptium\jdk-17\bin\javac.exe");

        var detector = new JavaDetector(reader, locator, runner, fileSystem);
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.DetectionSources.Count);
        Assert.Contains("Registry", row.DetectionSources);
        Assert.Contains("Command", row.DetectionSources);
        Assert.Null(row.ConflictNote);
    }

    [Fact]
    public async Task DetectAsync_RegistryAndCommandDisagreeOnVersion_RecordsConflictNote()
    {
        var reader = new FakeWindowsRegistryReader();
        reader.SetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\JavaSoft\JDK", "17.0.9");
        reader.SetValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\JavaSoft\JDK\17.0.9",
            new Dictionary<string, object?> { ["JavaHome"] = JavaHome });

        var locator = new FakeExecutableLocator(new() { ["java.exe"] = JavaPath });
        var runner = new FakeProcessRunner(new()
        {
            [$"{JavaPath}|-version"] = ProcessResult.Ok(0, string.Empty, OpenJdkVersionOutput, TimeSpan.Zero) // reports 17.0.12
        });
        var fileSystem = new FakeFileSystemReader();
        fileSystem.AddExisting(@"C:\Program Files\Eclipse Adoptium\jdk-17\bin\javac.exe");

        var detector = new JavaDetector(reader, locator, runner, fileSystem);
        var result = await detector.DetectAsync(CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.NotNull(row.ConflictNote);
        Assert.Contains("17.0.9", row.ConflictNote);
        Assert.Contains("17.0.12", row.ConflictNote);
    }

    [Fact]
    public async Task DetectAsync_NothingFound_ReturnsNotDetected()
    {
        var detector = new JavaDetector(new FakeWindowsRegistryReader(), new FakeExecutableLocator(new()), new FakeProcessRunner(new()), new FakeFileSystemReader());
        var result = await detector.DetectAsync(CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
    }
}
