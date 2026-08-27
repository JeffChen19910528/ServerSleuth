using ServerSleuth.Analysis.Migration.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>Ordinal-ordering sanity for the ascending <see cref="MigrationStatus"/> enum —
/// see skill.md (Phase 8A) §2.</summary>
public class MigrationStatusTests
{
    [Fact]
    public void AscendingOrder_ReadyIsLowest_BlockedIsHighest()
    {
        Assert.True(MigrationStatus.Ready < MigrationStatus.ReadyWithConditions);
        Assert.True(MigrationStatus.ReadyWithConditions < MigrationStatus.NeedsRemediation);
        Assert.True(MigrationStatus.NeedsRemediation < MigrationStatus.Blocked);
    }

    [Fact]
    public void MaxOfMixedStatuses_IsTheWorstOne()
    {
        var statuses = new[] { MigrationStatus.Ready, MigrationStatus.Blocked, MigrationStatus.ReadyWithConditions };
        Assert.Equal(MigrationStatus.Blocked, statuses.Max());
    }
}
