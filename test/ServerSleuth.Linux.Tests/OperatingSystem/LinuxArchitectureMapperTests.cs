using ServerSleuth.Core.Enums;
using ServerSleuth.Linux.OperatingSystem;

namespace ServerSleuth.Linux.Tests.OperatingSystem;

public class LinuxArchitectureMapperTests
{
    [Theory]
    [InlineData("x86_64", EntityArchitecture.X64)]
    [InlineData("amd64", EntityArchitecture.X64)]
    [InlineData("i686", EntityArchitecture.X86)]
    [InlineData("aarch64", EntityArchitecture.Arm64)]
    [InlineData("armv7l", EntityArchitecture.Arm)]
    [InlineData("riscv64", EntityArchitecture.Unknown)]
    [InlineData(null, EntityArchitecture.Unknown)]
    public void FromUname_MapsKnownValues(string? machine, EntityArchitecture expected)
    {
        Assert.Equal(expected, LinuxArchitectureMapper.FromUname(machine));
    }
}
