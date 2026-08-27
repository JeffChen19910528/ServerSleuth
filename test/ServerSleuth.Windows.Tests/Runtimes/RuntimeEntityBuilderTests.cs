using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Windows.Runtimes;
using CoreRuntime = ServerSleuth.Core.Models.Runtime;
using CoreSdk = ServerSleuth.Core.Models.Sdk;

namespace ServerSleuth.Windows.Tests.Runtimes;

public class RuntimeEntityBuilderTests
{
    private static RuntimeDetectionRow MakeRow(
        RuntimeEntityKind kind = RuntimeEntityKind.Runtime,
        IReadOnlyList<string>? sources = null,
        bool executableAvailable = false) => new()
    {
        Family = "Python",
        EntityKind = kind,
        Name = "Python",
        Version = "3.12.1",
        ExecutablePath = @"C:\Python312\python.exe",
        ExecutableAvailable = executableAvailable,
        DetectionSources = sources ?? ["Command"],
        Command = "python --version"
    };

    [Fact]
    public void Build_SdkKind_ProducesSdkEntity()
    {
        var entity = RuntimeEntityBuilder.Build(MakeRow(kind: RuntimeEntityKind.Sdk));

        Assert.IsType<CoreSdk>(entity);
    }

    [Fact]
    public void Build_RuntimeKind_ProducesRuntimeEntity()
    {
        var entity = RuntimeEntityBuilder.Build(MakeRow(kind: RuntimeEntityKind.Runtime));

        Assert.IsType<CoreRuntime>(entity);
    }

    [Fact]
    public void Build_StatusIsAlwaysInstalled_NeverUsed()
    {
        var entity = RuntimeEntityBuilder.Build(MakeRow());

        Assert.Equal(EntityStatus.Installed, entity.Status);
    }

    [Fact]
    public void Build_CommandSourceWithExecutableAvailable_IsVeryHighConfidence()
    {
        var entity = RuntimeEntityBuilder.Build(MakeRow(sources: ["Command"], executableAvailable: true));

        Assert.Equal(ConfidenceBand.VeryHigh, entity.Confidence.Band);
    }

    [Fact]
    public void Build_RegistryOnlySource_IsHighConfidenceNotVeryHigh()
    {
        var entity = RuntimeEntityBuilder.Build(MakeRow(sources: ["Registry"], executableAvailable: false));

        Assert.Equal(ConfidenceBand.High, entity.Confidence.Band);
    }

    [Fact]
    public void Build_NeitherRegistryNorConfirmedCommand_IsMediumConfidence()
    {
        var entity = RuntimeEntityBuilder.Build(MakeRow(sources: ["KnownPath"], executableAvailable: false));

        Assert.Equal(ConfidenceBand.Medium, entity.Confidence.Band);
    }

    [Fact]
    public void Build_RecordsOneEvidencePerDetectionSource()
    {
        var row = MakeRow() with { DetectionSources = ["Registry", "Command"], RegistryPath = @"HKLM\Foo" };

        var entity = RuntimeEntityBuilder.Build(row);

        Assert.Equal(2, entity.Evidence.Count);
        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.Registry);
        Assert.Contains(entity.Evidence, e => e.Type == EvidenceType.Command);
    }

    [Fact]
    public void Build_ConflictNote_IsRecordedAsMetadata()
    {
        var row = MakeRow() with { ConflictNote = "Registry reports 17, executable reports 21." };

        var entity = RuntimeEntityBuilder.Build(row);

        Assert.Equal("Registry reports 17, executable reports 21.", entity.Metadata["ConflictNote"]);
    }

    [Fact]
    public void Build_EnvironmentVariables_ArePrefixedInMetadata()
    {
        var row = MakeRow() with { EnvironmentVariables = new Dictionary<string, string> { ["GOROOT"] = @"C:\Go" } };

        var entity = RuntimeEntityBuilder.Build(row);

        Assert.Equal(@"C:\Go", entity.Metadata["Env.GOROOT"]);
    }

    [Fact]
    public void Build_DifferentVersionsSameFamily_ProduceDistinctIds()
    {
        var v1 = RuntimeEntityBuilder.Build(MakeRow() with { Version = "3.11.0" });
        var v2 = RuntimeEntityBuilder.Build(MakeRow() with { Version = "3.12.1" });

        Assert.NotEqual(v1.Id, v2.Id);
    }

    [Fact]
    public void Build_EditionRecordedWhenPresent_OmittedWhenAbsent()
    {
        var withEdition = RuntimeEntityBuilder.Build(MakeRow() with { Edition = "Eclipse Temurin" });
        Assert.Equal("Eclipse Temurin", withEdition.Metadata["Edition"]);

        var withoutEdition = RuntimeEntityBuilder.Build(MakeRow() with { Edition = null });
        Assert.False(withoutEdition.Metadata.ContainsKey("Edition"));
    }
}
