namespace ServerSleuth.Analysis.Correlation;

/// <summary>
/// Parses a raw command-line-shaped string (e.g. a Service's registry ImagePath, which may be
/// "C:\Foo\bar.exe" -k netsvcs, or an unquoted C:\Foo\bar.exe) into an executable path and
/// trailing arguments — never guessing when the split is genuinely ambiguous. This mirrors the
/// same quoted/ambiguous-path handling as ServerSleuth.Windows.COM.ServerReference (Phase 4B),
/// but is a separate, platform-agnostic implementation: ServerReference parses COM registry
/// values at scan time inside the Windows layer, while this parses generic executable
/// references at correlation time inside the platform-agnostic Analysis layer — reusing the
/// same proven algorithm rather than taking a cross-layer dependency on Windows-specific code.
/// See skill.md §5-6.
/// </summary>
public sealed record CommandLineReference
{
    public string? ExecutablePath { get; init; }
    public string? Arguments { get; init; }
    public bool RawReferenceDetected { get; init; }
    public required string RawValue { get; init; }

    public static CommandLineReference Parse(string rawValue)
    {
        var trimmed = rawValue.Trim();

        if (trimmed.Length == 0)
        {
            return new CommandLineReference { RawValue = rawValue, RawReferenceDetected = true };
        }

        if (trimmed[0] == '"')
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                var path = trimmed[1..closingQuote];
                var remainder = trimmed[(closingQuote + 1)..].Trim();
                return new CommandLineReference
                {
                    RawValue = rawValue,
                    ExecutablePath = path,
                    Arguments = remainder.Length > 0 ? remainder : null
                };
            }

            // Unterminated quote — malformed, don't guess.
            return new CommandLineReference { RawValue = rawValue, RawReferenceDetected = true };
        }

        if (!trimmed.Contains(' '))
        {
            // No ambiguity possible: the whole value is the path.
            return new CommandLineReference { RawValue = rawValue, ExecutablePath = trimmed };
        }

        // Unquoted value containing a space is ambiguous — it could be an unquoted path
        // containing a space, or a path followed by arguments. Never guess which.
        return new CommandLineReference { RawValue = rawValue, RawReferenceDetected = true };
    }
}
