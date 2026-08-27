using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Infrastructure.Configuration;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Binaries;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Configuration;

namespace ServerSleuth.Windows.Tests.Binaries;

public class WindowsBinaryDiscoveryScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    private static ScanRoot MakeRoot(string source, string ownerId, string reason, double confidence = 0.99) => new()
    {
        Path = @"D:\ERP", Source = source, OwnerEntityId = ownerId, Reason = reason, Confidence = new Confidence(confidence)
    };

    private static BinaryDiscoveryRow MakeRow(
        BinaryFileStatus status = BinaryFileStatus.Found,
        IReadOnlyList<ScanRoot>? roots = null,
        PeAnalysisResult? pe = null) => new()
    {
        Path = @"D:\ERP\Vendor.dll",
        FileName = "Vendor.dll",
        Extension = ".dll",
        FileStatus = status,
        SizeBytes = 2048,
        ContributingRoots = roots ?? [MakeRoot("IIS", "iis-app:ERP", "IIS Application PhysicalPath")],
        PeAnalysis = pe
    };

    [Fact]
    public void BuildEntity_FoundFile_MapsPathAndStatus()
    {
        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(), Redactor);

        Assert.Equal(@"D:\ERP\Vendor.dll", entity.Path);
        Assert.Equal(EntityStatus.Referenced, entity.Status);
        Assert.Equal("Found", entity.Metadata["FileStatus"]);
    }

    [Fact]
    public void BuildEntity_MissingFile_MapsStatusToUnknownButStillProducesEntity()
    {
        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(status: BinaryFileStatus.NotFound), Redactor);

        Assert.Equal(EntityStatus.Unknown, entity.Status);
        Assert.Equal("NotFound", entity.Metadata["FileStatus"]);
    }

    [Fact]
    public void BuildEntity_MultipleContributingRoots_MergeIntoOneEntityWithAllEvidence()
    {
        var roots = new[]
        {
            MakeRoot("IIS", "iis-app:ERP", "IIS Application PhysicalPath"),
            MakeRoot("COM", "com-1", "COM InprocServer32 path")
        };

        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(roots: roots), Redactor);

        Assert.Equal(2, entity.Evidence.Count);
        Assert.Contains("iis-app:ERP", entity.ReferencedByEntityIds);
        Assert.Contains("com-1", entity.ReferencedByEntityIds);
    }

    [Fact]
    public void BuildEntity_ManagedPe_RecordsBinaryTypeAndIsManaged()
    {
        var pe = new PeAnalysisResult
        {
            Status = PeParseStatus.Parsed,
            BinaryType = BinaryType.ManagedDll,
            IsManaged = true,
            Machine = "I386",
            Architecture = EntityArchitecture.X86,
            Imports = ["mscoree.dll"]
        };

        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(pe: pe), Redactor);

        Assert.Equal("ManagedDll", entity.Type);
        Assert.Equal("True", entity.Metadata["IsManaged"]);
        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.PeMetadata);
    }

    [Fact]
    public void BuildEntity_NativePeWithImports_RecordsImportsRedacted()
    {
        var pe = new PeAnalysisResult
        {
            Status = PeParseStatus.Parsed,
            BinaryType = BinaryType.NativeDll,
            IsManaged = false,
            Imports = ["KERNEL32.dll", "USER32.dll"]
        };

        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(pe: pe), Redactor);

        Assert.Contains("KERNEL32.dll", entity.Metadata["Imports"]);
        Assert.Contains("USER32.dll", entity.Metadata["Imports"]);
    }

    [Fact]
    public void BuildEntity_NoPeAnalysis_OmitsPeMetadataWithoutThrowing()
    {
        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(pe: null), Redactor);

        Assert.False(entity.Metadata.ContainsKey("BinaryType"));
        Assert.DoesNotContain(entity.Evidence, e => e.Type == EvidenceType.PeMetadata);
    }

    [Fact]
    public void BuildEntity_DelayImportsUnsupported_RecordsUnsupportedNotFailure()
    {
        var pe = new PeAnalysisResult { Status = PeParseStatus.Parsed, DelayImportsSupported = false };

        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(pe: pe), Redactor);

        Assert.Equal("Unsupported", entity.Metadata["DelayImportAnalysis"]);
    }

    [Fact]
    public void BuildEntity_HighestContributingRootConfidence_IsUsed()
    {
        var roots = new[]
        {
            MakeRoot("IIS", "iis-app:ERP", "IIS Application PhysicalPath", confidence: 0.75),
            MakeRoot("COM", "com-1", "COM InprocServer32 path", confidence: 0.99)
        };

        var entity = ServerSleuth.Windows.Binaries.WindowsBinaryDiscoveryScanner.BuildEntity(MakeRow(roots: roots), Redactor);

        Assert.Equal(0.99, entity.Confidence.Value);
    }
}
