using ServerSleuth.Analysis.Migration.Models;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>Ordinal-ordering sanity for the ascending <see cref="MigrationStatusImpact"/> enum
/// — see skill.md (Phase 8A) §1/§5.</summary>
public class MigrationStatusImpactTests
{
    [Fact]
    public void AscendingOrder_InformationalIsLowest_BlockingIsHighest()
    {
        Assert.True(MigrationStatusImpact.Informational < MigrationStatusImpact.Unclassified);
        Assert.True(MigrationStatusImpact.Unclassified < MigrationStatusImpact.Conditional);
        Assert.True(MigrationStatusImpact.Conditional < MigrationStatusImpact.RemediationRequired);
        Assert.True(MigrationStatusImpact.RemediationRequired < MigrationStatusImpact.Blocking);
    }
}
