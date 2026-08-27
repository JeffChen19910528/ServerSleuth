namespace ServerSleuth.Core.Evidence;

/// <summary>
/// A 0.00-1.00 confidence value with a fixed band mapping — see skill.md §22.
/// Never represents inference as fact: a Confidence always carries an explicit value,
/// there is no implicit "assume certain" default.
/// </summary>
public readonly struct Confidence : IEquatable<Confidence>
{
    public double Value { get; }

    public Confidence(double value)
    {
        if (value is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Confidence must be between 0.00 and 1.00 inclusive.");
        }

        Value = value;
    }

    public ConfidenceBand Band => Value switch
    {
        >= 0.90 => ConfidenceBand.VeryHigh,
        >= 0.75 => ConfidenceBand.High,
        >= 0.50 => ConfidenceBand.Medium,
        >= 0.25 => ConfidenceBand.Low,
        _ => ConfidenceBand.VeryLow
    };

    public static Confidence VeryHigh(double value = 1.0) => new(value);
    public static Confidence High(double value = 0.8) => new(value);
    public static Confidence Medium(double value = 0.6) => new(value);
    public static Confidence Low(double value = 0.35) => new(value);
    public static Confidence VeryLow(double value = 0.1) => new(value);

    public bool Equals(Confidence other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is Confidence other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"{Value:0.00} ({Band})";

    public static bool operator ==(Confidence left, Confidence right) => left.Equals(right);
    public static bool operator !=(Confidence left, Confidence right) => !left.Equals(right);
}
