using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Core.Tests.Models;

public class DiscoveryEntityTests
{
    private static Software CreateSoftware() => new()
    {
        Id = "software-1",
        Name = "Oracle Client",
        Type = "Software",
        Source = "Registry"
    };

    [Fact]
    public void NewEntity_DefaultsToUnknownStatusAndNoEvidence()
    {
        var entity = CreateSoftware();

        Assert.Equal(EntityStatus.Unknown, entity.Status);
        Assert.Empty(entity.Evidence);
    }

    [Fact]
    public void AddEvidence_AccumulatesMultipleRecords()
    {
        var entity = CreateSoftware();

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.Registry, Location = @"HKLM\Software\Oracle" });
        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.FileSystem, Location = @"C:\Oracle\product\19.3" });
        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.Process, Location = "ERPService.exe" });

        Assert.Equal(3, entity.Evidence.Count);
        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.Process);
    }

    [Fact]
    public void AddTag_DeduplicatesCaseInsensitively()
    {
        var entity = CreateSoftware();

        entity.AddTag("legacy");
        entity.AddTag("Legacy");
        entity.AddTag("erp");

        Assert.Equal(2, entity.Tags.Count);
    }

    [Fact]
    public void SetMetadata_OverwritesExistingKey()
    {
        var entity = CreateSoftware();

        entity.SetMetadata("architecture-hint", "x86");
        entity.SetMetadata("architecture-hint", "x64");

        Assert.Equal("x64", entity.Metadata["architecture-hint"]);
    }

    [Fact]
    public void Confidence_CanBeAssignedAfterConstruction()
    {
        var entity = CreateSoftware();

        entity.Confidence = Confidence.High();
        entity.Status = EntityStatus.Installed;

        Assert.Equal(ConfidenceBand.High, entity.Confidence.Band);
        Assert.Equal(EntityStatus.Installed, entity.Status);
    }
}
