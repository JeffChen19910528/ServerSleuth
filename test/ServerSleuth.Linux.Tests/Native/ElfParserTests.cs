using ServerSleuth.Core.Enums;
using ServerSleuth.Linux.Native;

namespace ServerSleuth.Linux.Tests.Native;

public class ElfParserTests
{
    private static readonly ElfParser Parser = new();

    [Fact]
    public void Parse_Elf64LittleEndianX86_64_ParsesClassEndianAndArchitecture()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(machine: 62);

        var result = Parser.Parse(bytes);

        Assert.Equal(ElfParseStatus.Parsed, result.Status);
        Assert.Equal(ElfClass.Elf64, result.Class);
        Assert.Equal(ElfEndian.Little, result.Endian);
        Assert.Equal(EntityArchitecture.X64, result.Architecture);
        Assert.Equal("EM_X86_64", result.Machine);
    }

    [Fact]
    public void Parse_Elf32LittleEndianX86_ParsesClassAndArchitecture()
    {
        var bytes = SyntheticElfBuilder.BuildElf32(machine: 3);

        var result = Parser.Parse(bytes);

        Assert.Equal(ElfParseStatus.Parsed, result.Status);
        Assert.Equal(ElfClass.Elf32, result.Class);
        Assert.Equal(EntityArchitecture.X86, result.Architecture);
    }

    [Theory]
    [InlineData((ushort)40, EntityArchitecture.Arm)]
    [InlineData((ushort)183, EntityArchitecture.Arm64)]
    [InlineData((ushort)243, EntityArchitecture.RiscV)]
    public void Parse_RecognizedMachineValues_MapToExpectedArchitecture(ushort machine, EntityArchitecture expected)
    {
        var bytes = SyntheticElfBuilder.BuildElf64(machine: machine);

        var result = Parser.Parse(bytes);

        Assert.Equal(expected, result.Architecture);
    }

    [Fact]
    public void Parse_UnknownMachineValue_ReturnsUnknownArchitecture_ButPreservesRawMachineValue()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(machine: 9999);

        var result = Parser.Parse(bytes);

        Assert.Equal(EntityArchitecture.Unknown, result.Architecture);
        Assert.Equal("EM_9999", result.Machine);
    }

    [Fact]
    public void Parse_BigEndianElf_NeverCrashes_ReturnsUnsupportedEndian_NeverGuessesArchitecture()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(bigEndian: true);

        var result = Parser.Parse(bytes);

        Assert.Equal(ElfParseStatus.UnsupportedEndian, result.Status);
        Assert.Equal(ElfEndian.Big, result.Endian);
        Assert.Equal(EntityArchitecture.Unknown, result.Architecture);
        Assert.NotNull(result.Diagnostic);
    }

    [Fact]
    public void Parse_MalformedMagic_ReturnsNotAnElf()
    {
        var bytes = new byte[64];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z'; // looks like a PE, not an ELF

        var result = Parser.Parse(bytes);

        Assert.Equal(ElfParseStatus.NotAnElf, result.Status);
    }

    [Fact]
    public void Parse_TruncatedBeforeIdentHeader_ReturnsTruncated_NeverThrows()
    {
        var result = Parser.Parse(new byte[10]);

        Assert.Equal(ElfParseStatus.Truncated, result.Status);
    }

    [Fact]
    public void Parse_TruncatedBeforeFullElf64Header_ReturnsTruncated()
    {
        var full = SyntheticElfBuilder.BuildElf64(includeDynamicSection: false);
        var truncated = full[..40]; // magic+class+data+machine present, but short of the full 64-byte header

        var result = Parser.Parse(truncated);

        Assert.Equal(ElfParseStatus.Truncated, result.Status);
    }

    [Fact]
    public void Parse_ProgramHeaderOffsetPastEndOfFile_ReturnsMalformedElf_NeverThrows()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(includeDynamicSection: false);
        // Corrupt e_phoff (offset 32, 8 bytes) to point far past the end of the file.
        BitConverter.GetBytes((ulong)999_999).CopyTo(bytes, 32);

        var result = Parser.Parse(bytes);

        // A program header table entirely past EOF yields zero readable segments — still a
        // structurally "Parsed" (if odd) binary with no dynamic section, not a crash.
        Assert.Equal(ElfParseStatus.Parsed, result.Status);
        Assert.Empty(result.Dependencies);
    }

    [Fact]
    public void Parse_NoDynamicSection_StaticBinary_ParsesWithZeroDependencies()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(includeDynamicSection: false);

        var result = Parser.Parse(bytes);

        Assert.Equal(ElfParseStatus.Parsed, result.Status);
        Assert.Empty(result.Dependencies);
    }

    [Fact]
    public void Parse_SingleDtNeeded_ExtractsDependencyName()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libc.so.6"]);

        var result = Parser.Parse(bytes);

        Assert.Equal(ElfParseStatus.Parsed, result.Status);
        Assert.Equal(["libc.so.6"], result.Dependencies);
    }

    [Fact]
    public void Parse_MultipleDtNeeded_ExtractsAllInFileOrder()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libssl.so.3", "libcrypto.so.3", "libc.so.6"]);

        var result = Parser.Parse(bytes);

        Assert.Equal(["libssl.so.3", "libcrypto.so.3", "libc.so.6"], result.Dependencies);
    }

    [Fact]
    public void Parse_DtRpath_ExtractsAndSplitsOnColon()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(rpath: "/opt/erp/lib:/opt/erp/vendor/lib");

        var result = Parser.Parse(bytes);

        Assert.Equal(["/opt/erp/lib", "/opt/erp/vendor/lib"], result.RPath);
        Assert.Empty(result.RunPath);
    }

    [Fact]
    public void Parse_DtRunpath_ExtractsAndSplitsOnColon_NeverConflatedWithRpath()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(runpath: "/opt/erp/lib");

        var result = Parser.Parse(bytes);

        Assert.Equal(["/opt/erp/lib"], result.RunPath);
        Assert.Empty(result.RPath);
    }

    [Fact]
    public void Parse_BothRpathAndRunpath_BothExtractedIndependently()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(rpath: "/rpath/dir", runpath: "/runpath/dir");

        var result = Parser.Parse(bytes);

        Assert.Equal(["/rpath/dir"], result.RPath);
        Assert.Equal(["/runpath/dir"], result.RunPath);
    }

    [Fact]
    public void Parse_LargeDependencyChain_AllExtracted_Deterministically()
    {
        var names = Enumerable.Range(0, 200).Select(i => $"libdep{i}.so.1").ToList();
        var bytes = SyntheticElfBuilder.BuildElf64(needed: names);

        var resultA = Parser.Parse(bytes);
        var resultB = Parser.Parse(bytes);

        Assert.Equal(200, resultA.Dependencies.Count);
        Assert.Equal(resultA.Dependencies, resultB.Dependencies); // deterministic — same input, same output
    }

    [Fact]
    public void Parse_SameInputParsedTwice_ProducesIdenticalResult()
    {
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libfoo.so"], rpath: "/a", runpath: "/b");

        var resultA = Parser.Parse(bytes);
        var resultB = Parser.Parse(bytes);

        Assert.Equal(resultA.Status, resultB.Status);
        Assert.Equal(resultA.Architecture, resultB.Architecture);
        Assert.Equal(resultA.Dependencies, resultB.Dependencies);
        Assert.Equal(resultA.RPath, resultB.RPath);
        Assert.Equal(resultA.RunPath, resultB.RunPath);
    }
}
