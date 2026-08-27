using ServerSleuth.Core.Models;
using ServerSleuth.Windows.Binaries;

namespace ServerSleuth.Windows.Tests.Binaries;

public class ComScanRootCollectorTests
{
    private static ComComponent MakeComponent(string id, string? inproc = null, string? local = null) => new()
    {
        Id = id, Name = id, Type = "ComComponent", Source = "Test", Clsid = "{GUID}",
        InprocServer32 = inproc, LocalServer32 = local
    };

    [Fact]
    public void Collect_InprocServer32_ProducesRootFromDirectory()
    {
        var roots = ComScanRootCollector.Collect([MakeComponent("com-1", inproc: @"C:\Vendor\Component.dll")]);

        var root = Assert.Single(roots);
        Assert.Equal(@"C:\Vendor", root.Path);
        Assert.Equal("COM", root.Source);
        Assert.Equal("com-1", root.OwnerEntityId);
    }

    [Fact]
    public void Collect_LocalServer32_ProducesRootFromDirectory()
    {
        var roots = ComScanRootCollector.Collect([MakeComponent("com-1", local: @"C:\Vendor\Server.exe")]);

        var root = Assert.Single(roots);
        Assert.Equal(@"C:\Vendor", root.Path);
        Assert.Equal("COM LocalServer32 path", root.Reason);
    }

    [Fact]
    public void Collect_BothServersInSameDirectory_AreDeduplicated()
    {
        var roots = ComScanRootCollector.Collect([MakeComponent("com-1", inproc: @"C:\Vendor\A.dll", local: @"C:\Vendor\B.exe")]);

        Assert.Single(roots);
    }

    [Fact]
    public void Collect_NoServerPaths_ProducesNoRoots()
    {
        var roots = ComScanRootCollector.Collect([MakeComponent("com-1")]);

        Assert.Empty(roots);
    }

    [Fact]
    public void Collect_MultipleComponentsInDifferentDirectories_ProduceDistinctRoots()
    {
        var roots = ComScanRootCollector.Collect(
        [
            MakeComponent("com-1", inproc: @"C:\Vendor1\A.dll"),
            MakeComponent("com-2", inproc: @"C:\Vendor2\B.dll")
        ]);

        Assert.Equal(2, roots.Count);
    }
}
