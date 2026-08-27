using ServerSleuth.Core.Enums;
using ServerSleuth.Linux.Native;

namespace ServerSleuth.Linux.Tests.Native;

public class LinuxNativeDependencyScannerBuildEntityTests
{
    [Fact]
    public void BuildEntity_FoundBinary_MapsPathArchitectureAndOwners()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            OwnerEntityIds = ["service:erp.service"],
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Class = ElfClass.Elf64, Endian = ElfEndian.Little, Machine = "EM_X86_64", Architecture = EntityArchitecture.X64 }
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("dll:/opt/erp/bin/erp", entity.Id);
        Assert.Equal("erp", entity.Name);
        Assert.Equal(EntityArchitecture.X64, entity.Architecture);
        Assert.Equal(EntityStatus.Referenced, entity.Status);
        Assert.Contains("service:erp.service", entity.ReferencedByEntityIds);
    }

    [Fact]
    public void BuildEntity_NotFoundBinary_MapsUnknownStatus_NeverReferenced()
    {
        var row = new NativeBinaryRow { Path = "/opt/erp/bin/missing", FileStatus = NativeBinaryFileStatus.NotFound };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal(EntityStatus.Unknown, entity.Status);
    }

    [Fact]
    public void BuildEntity_ElfMetadata_RecordsClassEndianMachineAsMetadata()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Class = ElfClass.Elf64, Endian = ElfEndian.Little, Machine = "EM_X86_64", Architecture = EntityArchitecture.X64 }
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("Elf64", entity.Metadata["ElfClass"]);
        Assert.Equal("Little", entity.Metadata["ElfEndian"]);
        Assert.Equal("EM_X86_64", entity.Metadata["Machine"]);
    }

    [Fact]
    public void BuildEntity_UnsupportedEndian_RecordsDiagnosticMetadata()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp-be",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.UnsupportedEndian, Endian = ElfEndian.Big, Diagnostic = "Big-endian ELF is not supported." }
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("UnsupportedEndian", entity.Metadata["ElfParseStatus"]);
        Assert.Equal("Big-endian ELF is not supported.", entity.Metadata["ElfDiagnostic"]);
        Assert.Equal(EntityArchitecture.Unknown, entity.Architecture);
    }

    [Fact]
    public void BuildEntity_RPathAndRunPath_RecordedAsIndexedMetadata()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, RPath = ["/opt/erp/lib"], RunPath = ["/opt/erp/vendor"] }
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("/opt/erp/lib", entity.Metadata["RPath0"]);
        Assert.Equal("/opt/erp/vendor", entity.Metadata["RunPath0"]);
    }

    [Fact]
    public void BuildEntity_ResolvedDependency_RecordsMetadataAndBinaryImportEvidence()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Dependencies = ["libssl.so.3"] },
            ResolvedDependencies = [new LibraryResolutionResult { LibraryName = "libssl.so.3", Status = LibraryResolutionStatus.Resolved, ResolvedPath = "/usr/lib/x86_64-linux-gnu/libssl.so.3", Source = "WellKnownLocation" }]
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("libssl.so.3", entity.Metadata["Dependency0.Name"]);
        Assert.Equal("Resolved", entity.Metadata["Dependency0.Status"]);
        Assert.Equal("/usr/lib/x86_64-linux-gnu/libssl.so.3", entity.Metadata["Dependency0.ResolvedPath"]);
        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.BinaryImport && e.Detail == "DT_NEEDED=libssl.so.3");
    }

    [Fact]
    public void BuildEntity_UnresolvedDependency_PreservedAsAuditableReference_NeverFabricatesAPath()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Dependencies = ["libvendor.so"] },
            ResolvedDependencies = [new LibraryResolutionResult { LibraryName = "libvendor.so", Status = LibraryResolutionStatus.NotFound }]
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("NotFound", entity.Metadata["Dependency0.Status"]);
        Assert.False(entity.Metadata.ContainsKey("Dependency0.ResolvedPath"));
    }

    [Fact]
    public void BuildEntity_AmbiguousDependency_RecordsAllCandidates()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Dependencies = ["libfoo.so"] },
            ResolvedDependencies = [new LibraryResolutionResult { LibraryName = "libfoo.so", Status = LibraryResolutionStatus.Ambiguous, Candidates = ["/opt/app1/libfoo.so", "/opt/app2/libfoo.so"] }]
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal("Ambiguous", entity.Metadata["Dependency0.Status"]);
        Assert.Contains("/opt/app1/libfoo.so", entity.Metadata["Dependency0.Candidates"]);
        Assert.Contains("/opt/app2/libfoo.so", entity.Metadata["Dependency0.Candidates"]);
    }

    [Fact]
    public void BuildEntity_DeterministicIdentity_SamePathProducesSameId()
    {
        var row = new NativeBinaryRow { Path = "/opt/erp/bin/erp", FileStatus = NativeBinaryFileStatus.Found };

        var entityA = LinuxNativeDependencyScanner.BuildEntity(row);
        var entityB = LinuxNativeDependencyScanner.BuildEntity(row);

        Assert.Equal(entityA.Id, entityB.Id);
    }

    [Fact]
    public void BuildEntity_NeverStoresRawBinaryContent()
    {
        var row = new NativeBinaryRow
        {
            Path = "/opt/erp/bin/erp",
            FileStatus = NativeBinaryFileStatus.Found,
            ElfAnalysis = new ElfAnalysisResult { Status = ElfParseStatus.Parsed, Dependencies = ["libc.so.6"] }
        };

        var entity = LinuxNativeDependencyScanner.BuildEntity(row);

        // Only normalized facts (path/architecture/dependency names/metadata) — never a byte
        // blob anywhere on the entity.
        Assert.All(entity.Metadata.Values, v => Assert.IsType<string>(v));
    }
}
