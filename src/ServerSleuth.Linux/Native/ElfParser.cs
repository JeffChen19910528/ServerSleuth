using System.Buffers.Binary;
using System.Text;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Linux.Native;

/// <summary>
/// Minimal, read-only ELF parser — implements exactly what skill.md (Phase 6F) §4 asks for
/// (magic/EI_CLASS/EI_DATA/e_machine/program headers/dynamic section/DT_NEEDED/DT_RPATH/
/// DT_RUNPATH) and nothing more: no disassembler, no DWARF, no symbol table analysis. Never
/// throws on malformed/truncated input — every failure path is caught and turned into a
/// classified <see cref="ElfParseStatus"/> instead, so one bad binary can never abort a scan.
/// </summary>
public sealed class ElfParser : ILinuxElfParser
{
    private const uint PtLoad = 1;
    private const uint PtDynamic = 2;

    private const long DtNull = 0;
    private const long DtNeeded = 1;
    private const long DtStrTab = 5;
    private const long DtStrSz = 10;
    private const long DtRpath = 15;
    private const long DtRunpath = 29;

    private readonly record struct LoadSegment(ulong VAddr, ulong FileOffset, ulong FileSize);
    private readonly record struct DynamicSegment(ulong FileOffset, ulong FileSize);

    public ElfAnalysisResult Parse(ReadOnlySpan<byte> data)
    {
        try
        {
            return ParseCore(data);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or OverflowException)
        {
            // A malformed/truncated file can make any offset/size field nonsensical — every
            // such failure degrades to MalformedElf rather than propagating, per skill.md §26
            // ("one unreadable binary must not fail the entire scan").
            return new ElfAnalysisResult { Status = ElfParseStatus.MalformedElf, Diagnostic = "ELF structure is internally inconsistent (an offset or size field is out of range)." };
        }
    }

    private static ElfAnalysisResult ParseCore(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20)
        {
            return new ElfAnalysisResult { Status = ElfParseStatus.Truncated, Diagnostic = "File is shorter than the fixed portion of an ELF identification header." };
        }

        if (data[0] != 0x7F || data[1] != (byte)'E' || data[2] != (byte)'L' || data[3] != (byte)'F')
        {
            return new ElfAnalysisResult { Status = ElfParseStatus.NotAnElf, Diagnostic = "Missing ELF magic number." };
        }

        var elfClass = data[4] switch { 1 => ElfClass.Elf32, 2 => ElfClass.Elf64, _ => ElfClass.Unknown };
        var endian = data[5] switch { 1 => ElfEndian.Little, 2 => ElfEndian.Big, _ => ElfEndian.Unknown };

        if (elfClass == ElfClass.Unknown)
        {
            return new ElfAnalysisResult { Status = ElfParseStatus.MalformedElf, Class = elfClass, Endian = endian, Diagnostic = $"Unrecognized EI_CLASS value {data[4]}." };
        }

        if (endian != ElfEndian.Little)
        {
            // Deliberately stop here — e_machine/program headers/dynamic section are all
            // multi-byte fields whose byte order depends on EI_DATA, and this parser does not
            // implement big-endian field decoding. Never guess. See skill.md §6.
            return new ElfAnalysisResult
            {
                Status = ElfParseStatus.UnsupportedEndian,
                Class = elfClass,
                Endian = endian,
                Diagnostic = "Big-endian ELF is not supported by this parser; header fields beyond EI_CLASS/EI_DATA were not decoded."
            };
        }

        var is64 = elfClass == ElfClass.Elf64;
        var headerSize = is64 ? 64 : 52;
        if (data.Length < headerSize)
        {
            return new ElfAnalysisResult { Status = ElfParseStatus.Truncated, Class = elfClass, Endian = endian, Diagnostic = "File is shorter than the full ELF header for its class." };
        }

        var machineValue = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(18, 2));
        var (machineName, architecture) = MapMachine(machineValue);

        ulong phoff;
        int phentsize, phnum;

        if (is64)
        {
            phoff = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(32, 8));
            phentsize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(54, 2));
            phnum = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(56, 2));
        }
        else
        {
            phoff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(28, 4));
            phentsize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(42, 2));
            phnum = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(44, 2));
        }

        var loadSegments = new List<LoadSegment>();
        DynamicSegment? dynamicSegment = null;

        for (var i = 0; i < phnum; i++)
        {
            var entryOffset = checked((long)phoff + (long)i * phentsize);
            var entry = ReadProgramHeader(data, entryOffset, is64);
            if (entry is null)
            {
                break; // program header table extends past the file — stop, keep what was already found
            }

            var (type, segOffset, segVAddr, segFileSize) = entry.Value;

            if (type == PtLoad)
            {
                loadSegments.Add(new LoadSegment(segVAddr, segOffset, segFileSize));
            }
            else if (type == PtDynamic)
            {
                dynamicSegment = new DynamicSegment(segOffset, segFileSize);
            }
        }

        if (dynamicSegment is null)
        {
            // No PT_DYNAMIC segment is a completely valid, common case (a statically linked
            // binary) — not an error, just zero dependencies.
            return new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Class = elfClass, Endian = endian, Machine = machineName, Architecture = architecture };
        }

        var (dependencies, rpath, runPath, partial, diagnostic) = ReadDynamicSection(data, dynamicSegment.Value, loadSegments, is64);

        return new ElfAnalysisResult
        {
            Status = partial ? ElfParseStatus.PartiallyParsed : ElfParseStatus.Parsed,
            Class = elfClass,
            Endian = endian,
            Machine = machineName,
            Architecture = architecture,
            Dependencies = dependencies,
            RPath = rpath,
            RunPath = runPath,
            Diagnostic = diagnostic
        };
    }

    private static (uint Type, ulong Offset, ulong VAddr, ulong FileSize)? ReadProgramHeader(ReadOnlySpan<byte> data, long entryOffset, bool is64)
    {
        var size = is64 ? 56 : 32;
        if (entryOffset < 0 || entryOffset + size > data.Length)
        {
            return null;
        }

        var entry = data.Slice((int)entryOffset, size);

        if (is64)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(entry[..4]);
            var offset = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(8, 8));
            var vaddr = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(16, 8));
            var fileSize = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(32, 8));
            return (type, offset, vaddr, fileSize);
        }
        else
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(entry[..4]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(4, 4));
            var vaddr = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(8, 4));
            var fileSize = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(16, 4));
            return (type, offset, vaddr, fileSize);
        }
    }

    private static (IReadOnlyList<string> Dependencies, IReadOnlyList<string> RPath, IReadOnlyList<string> RunPath, bool Partial, string? Diagnostic)
        ReadDynamicSection(ReadOnlySpan<byte> data, DynamicSegment dynamicSegment, List<LoadSegment> loadSegments, bool is64)
    {
        var dynEntrySize = is64 ? 16 : 8;
        var needed = new List<long>();
        long? rpathOffset = null;
        long? runpathOffset = null;
        ulong? strTabVAddr = null;
        ulong? strTabSize = null;

        var count = (long)dynamicSegment.FileSize / dynEntrySize;
        for (var i = 0; i < count; i++)
        {
            var entryOffset = checked((long)dynamicSegment.FileOffset + i * dynEntrySize);
            if (entryOffset < 0 || entryOffset + dynEntrySize > data.Length)
            {
                break;
            }

            var entry = data.Slice((int)entryOffset, dynEntrySize);
            long tag;
            ulong value;

            if (is64)
            {
                tag = BinaryPrimitives.ReadInt64LittleEndian(entry[..8]);
                value = BinaryPrimitives.ReadUInt64LittleEndian(entry.Slice(8, 8));
            }
            else
            {
                tag = BinaryPrimitives.ReadInt32LittleEndian(entry[..4]);
                value = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(4, 4));
            }

            if (tag == DtNull)
            {
                break;
            }

            switch (tag)
            {
                case DtNeeded: needed.Add((long)value); break;
                case DtRpath: rpathOffset = (long)value; break;
                case DtRunpath: runpathOffset = (long)value; break;
                case DtStrTab: strTabVAddr = value; break;
                case DtStrSz: strTabSize = value; break;
            }
        }

        if (strTabVAddr is null)
        {
            return ([], [], [], true, "Dynamic section present but DT_STRTAB was not found — dependency names could not be resolved.");
        }

        var strTabFileOffsetValue = TranslateVirtualAddress(strTabVAddr.Value, loadSegments);
        if (strTabFileOffsetValue is null)
        {
            return ([], [], [], true, "DT_STRTAB virtual address does not fall within any PT_LOAD segment — dependency names could not be resolved.");
        }

        var strTabFileOffset = (long)strTabFileOffsetValue.Value;
        var maxStringTableLength = strTabSize.HasValue ? (long)strTabSize.Value : data.Length - strTabFileOffset;
        var stringTableEnd = (long)Math.Min(strTabFileOffset + maxStringTableLength, data.Length);
        if (stringTableEnd <= strTabFileOffset)
        {
            return ([], [], [], true, "DT_STRTAB size resolves to an empty or invalid range.");
        }

        var stringTable = data[(int)strTabFileOffset..(int)stringTableEnd];

        var dependencies = new List<string>();
        foreach (var offset in needed)
        {
            var name = ReadCString(stringTable, offset);
            if (name is not null)
            {
                dependencies.Add(name);
            }
        }

        var rpathValue = rpathOffset is not null ? ReadCString(stringTable, rpathOffset.Value) : null;
        var runpathValue = runpathOffset is not null ? ReadCString(stringTable, runpathOffset.Value) : null;

        return (
            dependencies,
            SplitPathList(rpathValue),
            SplitPathList(runpathValue),
            false,
            null);
    }

    private static IReadOnlyList<string> SplitPathList(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(':', StringSplitOptions.RemoveEmptyEntries);

    private static string? ReadCString(ReadOnlySpan<byte> stringTable, long offset)
    {
        if (offset < 0 || offset >= stringTable.Length)
        {
            return null;
        }

        var remainder = stringTable[(int)offset..];
        var nullIndex = remainder.IndexOf((byte)0);
        var bytes = nullIndex >= 0 ? remainder[..nullIndex] : remainder;
        return bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes);
    }

    private static ulong? TranslateVirtualAddress(ulong vaddr, List<LoadSegment> loadSegments)
    {
        foreach (var segment in loadSegments)
        {
            if (vaddr >= segment.VAddr && vaddr < segment.VAddr + segment.FileSize)
            {
                return segment.FileOffset + (vaddr - segment.VAddr);
            }
        }

        return null;
    }

    private static (string Name, EntityArchitecture Architecture) MapMachine(ushort machineValue) => machineValue switch
    {
        3 => ("EM_386", EntityArchitecture.X86),
        40 => ("EM_ARM", EntityArchitecture.Arm),
        62 => ("EM_X86_64", EntityArchitecture.X64),
        183 => ("EM_AARCH64", EntityArchitecture.Arm64),
        243 => ("EM_RISCV", EntityArchitecture.RiscV),
        _ => ($"EM_{machineValue}", EntityArchitecture.Unknown)
    };
}
