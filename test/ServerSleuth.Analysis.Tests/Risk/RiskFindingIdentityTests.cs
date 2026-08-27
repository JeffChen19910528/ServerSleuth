using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Analysis.Tests.Risk;

public class RiskFindingIdentityTests
{
    [Fact]
    public void ComputeId_SameInputs_ProducesSameId()
    {
        var idA = RiskFinding.ComputeId("RR1-Test", "entity:1", ["related:1", "related:2"]);
        var idB = RiskFinding.ComputeId("RR1-Test", "entity:1", ["related:1", "related:2"]);

        Assert.Equal(idA, idB);
    }

    [Fact]
    public void ComputeId_RelatedIdsInDifferentOrder_ProducesSameId()
    {
        var idA = RiskFinding.ComputeId("RR1-Test", "entity:1", ["related:2", "related:1"]);
        var idB = RiskFinding.ComputeId("RR1-Test", "entity:1", ["related:1", "related:2"]);

        Assert.Equal(idA, idB);
    }

    [Fact]
    public void ComputeId_DuplicateRelatedIds_NeverDoubleCounted()
    {
        var idA = RiskFinding.ComputeId("RR1-Test", "entity:1", ["related:1", "related:1"]);
        var idB = RiskFinding.ComputeId("RR1-Test", "entity:1", ["related:1"]);

        Assert.Equal(idA, idB);
    }

    [Fact]
    public void ComputeId_DifferentSourceEntity_ProducesDifferentId()
    {
        var idA = RiskFinding.ComputeId("RR1-Test", "entity:1");
        var idB = RiskFinding.ComputeId("RR1-Test", "entity:2");

        Assert.NotEqual(idA, idB);
    }

    [Fact]
    public void ComputeId_DifferentRuleId_ProducesDifferentId()
    {
        var idA = RiskFinding.ComputeId("RR1-Test", "entity:1");
        var idB = RiskFinding.ComputeId("RR2-Test", "entity:1");

        Assert.NotEqual(idA, idB);
    }

    [Fact]
    public void ComputeId_FollowsTheDocumentedFormat_NeverAGuid()
    {
        var id = RiskFinding.ComputeId("RR1-Test", "entity:1", ["b", "a"]);

        Assert.Equal("risk:RR1-Test:entity:1:a,b", id);
        Assert.False(Guid.TryParse(id, out _));
    }
}
