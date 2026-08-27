using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Tests.Risk.Aggregation;

/// <summary>See skill.md (Phase 7B) §2, §6: ordinal ordering, never string sort.</summary>
public class AggregateSeverityTests
{
    [Fact]
    public void OrdinalOrdering_IsAscending_NoneThroughCritical()
    {
        Assert.True(AggregateSeverity.None < AggregateSeverity.Info);
        Assert.True(AggregateSeverity.Info < AggregateSeverity.Low);
        Assert.True(AggregateSeverity.Low < AggregateSeverity.Medium);
        Assert.True(AggregateSeverity.Medium < AggregateSeverity.High);
        Assert.True(AggregateSeverity.High < AggregateSeverity.Critical);
    }

    [Theory]
    [InlineData(RiskSeverity.Info, AggregateSeverity.Info)]
    [InlineData(RiskSeverity.Low, AggregateSeverity.Low)]
    [InlineData(RiskSeverity.Medium, AggregateSeverity.Medium)]
    [InlineData(RiskSeverity.High, AggregateSeverity.High)]
    [InlineData(RiskSeverity.Critical, AggregateSeverity.Critical)]
    public void ToAggregateSeverity_MapsEveryRiskSeverity_ToItsIdenticallyNamedCounterpart(RiskSeverity input, AggregateSeverity expected) =>
        Assert.Equal(expected, input.ToAggregateSeverity());
}
