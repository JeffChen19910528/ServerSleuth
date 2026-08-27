using ServerSleuth.Core.Enums;
using ServerSleuth.Windows.Binaries;

namespace ServerSleuth.Windows.Tests.Binaries;

/// <summary>Validates the PE analyzer against real files — a managed .NET assembly this
/// solution itself built, and a well-known native Windows system DLL. Never loads/executes
/// either file; only reads static PE headers via IPeAnalyzer.</summary>
public class PeAnalyzerRealFileTests
{
    private readonly PeAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_OwnManagedAssembly_IsDetectedAsManagedDll()
    {
        var path = typeof(ServerSleuth.Core.Models.Server).Assembly.Location;

        var result = _analyzer.Analyze(path);

        Assert.Equal(PeParseStatus.Parsed, result.Status);
        Assert.True(result.IsManaged);
        Assert.Equal(BinaryType.ManagedDll, result.BinaryType);
        // A pure-IL "AnyCPU" assembly conventionally reports Machine=I386 in its PE header
        // regardless of the actual target platform — the CLR, not the Machine field, is what
        // makes it platform-agnostic. X86 here is the correct, expected reading, not a bug.
        Assert.Equal(EntityArchitecture.X86, result.Architecture);
    }

    [Fact]
    public void Analyze_NativeSystemDll_IsDetectedAsNativeWithImports()
    {
        var path = @"C:\Windows\System32\kernel32.dll";

        var result = _analyzer.Analyze(path);

        Assert.Equal(PeParseStatus.Parsed, result.Status);
        Assert.False(result.IsManaged);
        Assert.Equal(BinaryType.NativeDll, result.BinaryType);
        Assert.NotEqual(EntityArchitecture.Unknown, result.Architecture);
        Assert.NotEmpty(result.Imports);
    }

    [Fact]
    public void Analyze_NativeExe_IsDetectedAsExe()
    {
        var path = @"C:\Windows\System32\notepad.exe";

        var result = _analyzer.Analyze(path);

        Assert.Equal(PeParseStatus.Parsed, result.Status);
        Assert.Equal(BinaryType.Exe, result.BinaryType);
    }

    [Fact]
    public void Analyze_NonExistentFile_ReturnsUnreadableNotThrow()
    {
        var result = _analyzer.Analyze(@"C:\this\path\does\not\exist\fake.dll");

        Assert.Equal(PeParseStatus.Unreadable, result.Status);
    }

    [Fact]
    public void Analyze_NonPeFile_ReturnsInvalidPeNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "not-a-pe-" + Guid.NewGuid() + ".dll");
        File.WriteAllText(path, "this is definitely not a PE file");

        try
        {
            var result = _analyzer.Analyze(path);

            Assert.Equal(PeParseStatus.InvalidPe, result.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
