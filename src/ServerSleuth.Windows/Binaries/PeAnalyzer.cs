using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Windows.Binaries;

public sealed class PeAnalyzer : IPeAnalyzer
{
    private const int MaxImportNameLength = 260; // MAX_PATH-ish sanity bound against corrupt data

    public PeAnalysisResult Analyze(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // PEStreamOptions.Default reads headers/sections on demand rather than loading the
            // whole file up front — see skill.md §23 (bounded reads, not huge byte arrays).
            using var peReader = new PEReader(stream);

            var headers = peReader.PEHeaders;
            if (headers?.PEHeader is null || headers.CoffHeader is null)
            {
                return new PeAnalysisResult { Status = PeParseStatus.InvalidPe };
            }

            var isManaged = peReader.HasMetadata;
            var machine = headers.CoffHeader.Machine;
            var is64Bit = headers.PEHeader.Magic == PEMagic.PE32Plus;

            return new PeAnalysisResult
            {
                Status = PeParseStatus.Parsed,
                BinaryType = DetermineBinaryType(filePath, isManaged, headers),
                IsManaged = isManaged,
                Machine = machine.ToString(),
                Architecture = MapMachine(machine),
                Is64BitImage = is64Bit,
                Subsystem = headers.PEHeader.Subsystem.ToString(),
                ImageSizeBytes = headers.PEHeader.SizeOfImage,
                TimestampUtc = headers.CoffHeader.TimeDateStamp > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(headers.CoffHeader.TimeDateStamp)
                    : null,
                Imports = TryReadImports(peReader, headers),
                DelayImportsSupported = false, // see skill.md §13 — explicitly permitted fallback
                DelayImports = []
            };
        }
        catch (BadImageFormatException)
        {
            return new PeAnalysisResult { Status = PeParseStatus.InvalidPe };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PeAnalysisResult { Status = PeParseStatus.Unreadable };
        }
    }

    private static BinaryType DetermineBinaryType(string filePath, bool isManaged, PEHeaders headers)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".ocx")
        {
            return BinaryType.Ocx;
        }

        if (headers.IsExe)
        {
            return BinaryType.Exe;
        }

        if (extension == ".dll")
        {
            return isManaged ? BinaryType.ManagedDll : BinaryType.NativeDll;
        }

        return BinaryType.UnknownPe;
    }

    private static EntityArchitecture MapMachine(Machine machine) => machine switch
    {
        Machine.I386 => EntityArchitecture.X86,
        Machine.Amd64 => EntityArchitecture.X64,
        Machine.Arm64 => EntityArchitecture.Arm64,
        Machine.Arm or Machine.ArmThumb2 => EntityArchitecture.Arm,
        _ => EntityArchitecture.Unknown
    };

    /// <summary>Walks the Import Directory Table to collect imported module names only — never
    /// recurses into the imported DLLs themselves (that is Phase 5's job). Returns an empty
    /// list, never throws, if the directory is absent or malformed.</summary>
    private static IReadOnlyList<string> TryReadImports(PEReader peReader, PEHeaders headers)
    {
        var directory = headers.PEHeader!.ImportTableDirectory;
        if (directory.Size == 0)
        {
            return [];
        }

        try
        {
            var imports = new List<string>();
            var block = peReader.GetSectionData(directory.RelativeVirtualAddress);
            var reader = block.GetReader();

            while (reader.RemainingBytes >= 20)
            {
                var originalFirstThunk = reader.ReadInt32();
                reader.ReadInt32(); // TimeDateStamp
                reader.ReadInt32(); // ForwarderChain
                var nameRva = reader.ReadInt32();
                var firstThunk = reader.ReadInt32();

                if (originalFirstThunk == 0 && nameRva == 0 && firstThunk == 0)
                {
                    break; // null terminator descriptor
                }

                if (nameRva == 0)
                {
                    continue;
                }

                var name = TryReadNullTerminatedAscii(peReader, nameRva);
                if (name is not null)
                {
                    imports.Add(name);
                }
            }

            return imports.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private static string? TryReadNullTerminatedAscii(PEReader peReader, int relativeVirtualAddress)
    {
        try
        {
            var block = peReader.GetSectionData(relativeVirtualAddress);
            var reader = block.GetReader();

            var bytes = new List<byte>();
            while (reader.RemainingBytes > 0 && bytes.Count < MaxImportNameLength)
            {
                var b = reader.ReadByte();
                if (b == 0)
                {
                    break;
                }

                bytes.Add(b);
            }

            return bytes.Count > 0 ? System.Text.Encoding.ASCII.GetString(bytes.ToArray()) : null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }
}
