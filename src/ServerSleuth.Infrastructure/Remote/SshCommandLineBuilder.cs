namespace ServerSleuth.Infrastructure.Remote;

/// <summary>
/// Builds the single command-line string the SSH "exec" channel requires — see skill.md
/// (Phase 10D-2) §8. The SSH protocol itself (RFC 4254 §6.5) has no argv-style exec request:
/// an "exec" request carries exactly one opaque string, which sshd hands to the authenticated
/// user's login shell to interpret. That shell hand-off happens entirely server-side, outside
/// this client's control — it is a protocol fact, not a design choice this codebase made. What
/// IS this codebase's choice is how that one string gets built: every executable name and
/// argument is POSIX single-quote-escaped independently and joined with a single space, so no
/// argument boundary can be crossed and no embedded shell metacharacter (space/quote/`;`/`|`/
/// `&amp;`/backtick/`$()`/newline/CRLF) can introduce additional shell syntax. This is the exact
/// same escaping strategy Python's <c>shlex.quote</c>/Ruby's <c>Shellwords.escape</c> use, and
/// is applied ONLY here, inside the transport — no scanner or provider ever builds a command
/// string itself (skill.md §7: <see cref="RemoteOperationKind.ProcessQuery"/> keeps
/// Executable/Arguments discrete all the way to this one point).
/// </summary>
public static class SshCommandLineBuilder
{
    /// <summary>
    /// Builds a safely-quoted command line from a discrete executable and argument list — never
    /// called with a caller-supplied raw string (there is no overload that accepts one).
    /// </summary>
    public static string Build(string executable, IReadOnlyList<string> arguments)
    {
        var parts = new List<string>(arguments.Count + 1) { Quote(executable) };
        parts.AddRange(arguments.Select(Quote));
        return string.Join(' ', parts);
    }

    /// <summary>
    /// POSIX single-quote escaping: wrap the value in single quotes, and replace every embedded
    /// single quote with <c>'\''</c> (close the quote, emit an escaped literal quote, reopen the
    /// quote). Inside single quotes, POSIX shells treat every other character — including
    /// newlines, backticks, `$()`, `;`, `|`, `&amp;`, and Unicode — as a literal byte, never as
    /// shell syntax. An empty string still produces a valid, non-empty token (`''`), so an empty
    /// argument can never disappear or merge with its neighbor.
    /// </summary>
    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
}
