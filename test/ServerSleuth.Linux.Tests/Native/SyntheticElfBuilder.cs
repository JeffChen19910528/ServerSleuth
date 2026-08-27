using System.Text;

namespace ServerSleuth.Linux.Tests.Native;

/// <summary>
/// Builds minimal, deterministic, synthetic ELF32/ELF64 byte arrays for testing `ElfParser`
/// without depending on any real machine binary — see skill.md (Phase 6F) §28 ("do not use real
/// machine binaries as the only test source; use deterministic binary fixtures"). Uses an
/// identity virtual-address mapping (PT_LOAD vaddr == file offset == 0, covering the whole
/// file) so DT_STRTAB's virtual address always translates to the correct file offset without
/// needing a real loader.
/// </summary>
internal static class SyntheticElfBuilder
{
    public static byte[] BuildElf64(
        ushort machine = 62, // EM_X86_64
        IReadOnlyList<string>? needed = null,
        string? rpath = null,
        string? runpath = null,
        bool includeDynamicSection = true,
        bool bigEndian = false) =>
        Build(is64: true, machine, needed ?? [], rpath, runpath, includeDynamicSection, bigEndian);

    public static byte[] BuildElf32(
        ushort machine = 3, // EM_386
        IReadOnlyList<string>? needed = null,
        string? rpath = null,
        string? runpath = null,
        bool includeDynamicSection = true) =>
        Build(is64: false, machine, needed ?? [], rpath, runpath, includeDynamicSection, bigEndian: false);

    private static byte[] Build(bool is64, ushort machine, IReadOnlyList<string> needed, string? rpath, string? runpath, bool includeDynamicSection, bool bigEndian)
    {
        var headerSize = is64 ? 64 : 52;
        var phEntrySize = is64 ? 56 : 32;
        var dynEntrySize = is64 ? 16 : 8;
        var phCount = includeDynamicSection ? 2 : 1;
        var phoff = headerSize;
        var dynOffset = phoff + phCount * phEntrySize;

        var (stringTable, neededOffsets, rpathOffset, runpathOffset) = BuildStringTable(needed, rpath, runpath);

        var dynEntries = new List<(long Tag, ulong Value)>();
        if (includeDynamicSection)
        {
            foreach (var offset in neededOffsets)
            {
                dynEntries.Add((1, (ulong)offset)); // DT_NEEDED
            }
            if (rpathOffset is { } rp) dynEntries.Add((15, (ulong)rp)); // DT_RPATH
            if (runpathOffset is { } rup) dynEntries.Add((29, (ulong)rup)); // DT_RUNPATH
        }

        // Two dynamic entries (DT_STRTAB, DT_STRSZ) are always appended after any RPATH/RUNPATH/
        // NEEDED entries, followed by the DT_NULL terminator.
        var stringTableFileOffset = dynOffset + (dynEntries.Count + 3) * dynEntrySize;
        if (includeDynamicSection)
        {
            dynEntries.Add((5, (ulong)stringTableFileOffset)); // DT_STRTAB (identity vaddr==offset)
            dynEntries.Add((10, (ulong)stringTable.Length)); // DT_STRSZ
            dynEntries.Add((0, 0)); // DT_NULL
        }

        var totalLength = includeDynamicSection ? stringTableFileOffset + stringTable.Length : phoff + phCount * phEntrySize;

        var buffer = new byte[totalLength];
        WriteHeader(buffer, is64, bigEndian, machine, phoff, phEntrySize, phCount);

        // PT_LOAD covering the entire file, identity-mapped (vaddr == file offset == 0).
        WriteProgramHeader(buffer, phoff, is64, type: 1, offset: 0, vaddr: 0, fileSize: (ulong)totalLength);

        if (includeDynamicSection)
        {
            var dynSize = (ulong)(dynEntries.Count * dynEntrySize);
            WriteProgramHeader(buffer, phoff + phEntrySize, is64, type: 2, offset: (ulong)dynOffset, vaddr: (ulong)dynOffset, fileSize: dynSize);
            WriteDynamicEntries(buffer, dynOffset, is64, dynEntries);
            Array.Copy(stringTable, 0, buffer, stringTableFileOffset, stringTable.Length);
        }

        return buffer;
    }

    private static void WriteHeader(byte[] buffer, bool is64, bool bigEndian, ushort machine, int phoff, int phEntrySize, int phCount)
    {
        buffer[0] = 0x7F;
        buffer[1] = (byte)'E';
        buffer[2] = (byte)'L';
        buffer[3] = (byte)'F';
        buffer[4] = is64 ? (byte)2 : (byte)1; // EI_CLASS
        buffer[5] = bigEndian ? (byte)2 : (byte)1; // EI_DATA

        if (bigEndian)
        {
            return; // deliberately leave the rest zeroed — the parser must stop right after EI_DATA for big-endian input
        }

        BitConverter.GetBytes(machine).CopyTo(buffer, 18);

        if (is64)
        {
            BitConverter.GetBytes((ulong)phoff).CopyTo(buffer, 32);
            BitConverter.GetBytes((ushort)phEntrySize).CopyTo(buffer, 54);
            BitConverter.GetBytes((ushort)phCount).CopyTo(buffer, 56);
        }
        else
        {
            BitConverter.GetBytes((uint)phoff).CopyTo(buffer, 28);
            BitConverter.GetBytes((ushort)phEntrySize).CopyTo(buffer, 42);
            BitConverter.GetBytes((ushort)phCount).CopyTo(buffer, 44);
        }
    }

    private static void WriteProgramHeader(byte[] buffer, int entryOffset, bool is64, uint type, ulong offset, ulong vaddr, ulong fileSize)
    {
        if (is64)
        {
            BitConverter.GetBytes(type).CopyTo(buffer, entryOffset);
            // flags at +4 left as zero
            BitConverter.GetBytes(offset).CopyTo(buffer, entryOffset + 8);
            BitConverter.GetBytes(vaddr).CopyTo(buffer, entryOffset + 16);
            // paddr at +24 left as zero
            BitConverter.GetBytes(fileSize).CopyTo(buffer, entryOffset + 32);
        }
        else
        {
            BitConverter.GetBytes(type).CopyTo(buffer, entryOffset);
            BitConverter.GetBytes((uint)offset).CopyTo(buffer, entryOffset + 4);
            BitConverter.GetBytes((uint)vaddr).CopyTo(buffer, entryOffset + 8);
            // paddr at +12 left as zero
            BitConverter.GetBytes((uint)fileSize).CopyTo(buffer, entryOffset + 16);
        }
    }

    private static void WriteDynamicEntries(byte[] buffer, int dynOffset, bool is64, List<(long Tag, ulong Value)> entries)
    {
        var entrySize = is64 ? 16 : 8;
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = dynOffset + i * entrySize;
            var (tag, value) = entries[i];

            if (is64)
            {
                BitConverter.GetBytes(tag).CopyTo(buffer, offset);
                BitConverter.GetBytes(value).CopyTo(buffer, offset + 8);
            }
            else
            {
                BitConverter.GetBytes((int)tag).CopyTo(buffer, offset);
                BitConverter.GetBytes((uint)value).CopyTo(buffer, offset + 4);
            }
        }
    }

    private static (byte[] StringTable, List<int> NeededOffsets, int? RPathOffset, int? RunPathOffset) BuildStringTable(
        IReadOnlyList<string> needed, string? rpath, string? runpath)
    {
        using var buffer = new MemoryStream();
        buffer.WriteByte(0); // offset 0 reserved as empty string, matching real ELF convention

        var neededOffsets = new List<int>();
        foreach (var name in needed)
        {
            neededOffsets.Add((int)buffer.Position);
            WriteCString(buffer, name);
        }

        int? rpathOffset = null;
        if (rpath is not null)
        {
            rpathOffset = (int)buffer.Position;
            WriteCString(buffer, rpath);
        }

        int? runpathOffset = null;
        if (runpath is not null)
        {
            runpathOffset = (int)buffer.Position;
            WriteCString(buffer, runpath);
        }

        return (buffer.ToArray(), neededOffsets, rpathOffset, runpathOffset);
    }

    private static void WriteCString(MemoryStream buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        buffer.Write(bytes, 0, bytes.Length);
        buffer.WriteByte(0);
    }
}
