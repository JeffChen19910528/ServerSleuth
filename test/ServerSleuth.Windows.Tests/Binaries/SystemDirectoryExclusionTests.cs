using ServerSleuth.Windows.Binaries;

namespace ServerSleuth.Windows.Tests.Binaries;

public class SystemDirectoryExclusionTests
{
    private static readonly string WindowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    [Fact]
    public void IsSystemOwned_System32Path_ReturnsTrue()
    {
        Assert.True(SystemDirectoryExclusion.IsSystemOwned(Path.Combine(WindowsDir, "System32")));
    }

    [Fact]
    public void IsSystemOwned_SysWow64Path_ReturnsTrue()
    {
        Assert.True(SystemDirectoryExclusion.IsSystemOwned(Path.Combine(WindowsDir, "SysWOW64")));
    }

    [Fact]
    public void IsSystemOwned_VendorApplicationPath_ReturnsFalse()
    {
        Assert.False(SystemDirectoryExclusion.IsSystemOwned(@"D:\Web\ERP\bin"));
    }

    [Fact]
    public void IsSystemOwned_ProgramFilesPath_ReturnsFalse()
    {
        Assert.False(SystemDirectoryExclusion.IsSystemOwned(@"C:\Program Files\Vendor"));
    }
}
