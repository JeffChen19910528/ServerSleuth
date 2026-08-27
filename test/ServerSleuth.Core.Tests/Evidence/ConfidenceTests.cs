using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Core.Tests.Evidence;

public class ConfidenceTests
{
    [Theory]
    [InlineData(1.00, ConfidenceBand.VeryHigh)]
    [InlineData(0.90, ConfidenceBand.VeryHigh)]
    [InlineData(0.89, ConfidenceBand.High)]
    [InlineData(0.75, ConfidenceBand.High)]
    [InlineData(0.74, ConfidenceBand.Medium)]
    [InlineData(0.50, ConfidenceBand.Medium)]
    [InlineData(0.49, ConfidenceBand.Low)]
    [InlineData(0.25, ConfidenceBand.Low)]
    [InlineData(0.24, ConfidenceBand.VeryLow)]
    [InlineData(0.00, ConfidenceBand.VeryLow)]
    public void Band_MatchesSkillMdBoundaries(double value, ConfidenceBand expectedBand)
    {
        var confidence = new Confidence(value);

        Assert.Equal(expectedBand, confidence.Band);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void Constructor_RejectsOutOfRangeValues(double invalidValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Confidence(invalidValue));
    }

    [Fact]
    public void Equality_IsBasedOnValue()
    {
        var a = new Confidence(0.5);
        var b = new Confidence(0.5);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }
}
