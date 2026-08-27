namespace ServerSleuth.Windows.COM;

/// <summary>
/// Result of parsing a raw InprocServer32/LocalServer32 default registry value into a path and
/// (for LocalServer32) trailing arguments. See skill.md §16 — path normalization must not
/// guess: an ambiguous unquoted value with spaces is reported as ambiguous rather than split
/// at a guessed boundary.
/// </summary>
public sealed record ServerReference
{
    public string? ExecutablePath { get; init; }
    public string? Arguments { get; init; }
    public bool RawReferenceDetected { get; init; }
    public required string RawValue { get; init; }

    public static ServerReference Parse(string rawValue)
    {
        var trimmed = rawValue.Trim();

        if (trimmed.Length == 0)
        {
            return new ServerReference { RawValue = rawValue, RawReferenceDetected = true };
        }

        if (trimmed[0] == '"')
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                var path = trimmed[1..closingQuote];
                var remainder = trimmed[(closingQuote + 1)..].Trim();
                return new ServerReference
                {
                    RawValue = rawValue,
                    ExecutablePath = path,
                    Arguments = remainder.Length > 0 ? remainder : null
                };
            }

            // Unterminated quote — malformed, don't guess.
            return new ServerReference { RawValue = rawValue, RawReferenceDetected = true };
        }

        if (!trimmed.Contains(' '))
        {
            // No ambiguity possible: the whole value is the path.
            return new ServerReference { RawValue = rawValue, ExecutablePath = trimmed };
        }

        // Unquoted value containing a space is ambiguous — it could be an unquoted path
        // containing a space, or a path followed by arguments. Never guess which.
        return new ServerReference { RawValue = rawValue, RawReferenceDetected = true };
    }
}
