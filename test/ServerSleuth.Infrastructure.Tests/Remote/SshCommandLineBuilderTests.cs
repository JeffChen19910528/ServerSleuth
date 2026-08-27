using ServerSleuth.Infrastructure.Remote;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>
/// Phase 10D-2 §8, §26: proves shell metacharacters embedded in an argument can never become
/// additional shell syntax. Since the deterministic suite must not depend on a live shell/SSH
/// server (skill.md §27), correctness is proven by round-tripping through a hand-written
/// reference POSIX single-quote UNquoter (a straightforward, independent reimplementation of the
/// same well-known algorithm — not the code under test) rather than by actually invoking
/// <c>/bin/sh</c>. A live-shell round-trip was additionally performed manually against the real
/// WSL2 Ubuntu host used for this phase's real-machine acceptance run — see PROGRESS.md's Phase
/// 10D-2 entry.
/// </summary>
public class SshCommandLineBuilderTests
{
    [Theory]
    [InlineData("hello world")]
    [InlineData("quote's here")]
    [InlineData("semi;colon")]
    [InlineData("pipe|here")]
    [InlineData("amp&ersand")]
    [InlineData("back`tick`")]
    [InlineData("dollar$(whoami)")]
    [InlineData("dollar${HOME}")]
    [InlineData("new\nline")]
    [InlineData("crlf\r\nhere")]
    [InlineData("redirect > /etc/passwd")]
    [InlineData("redirect2 < /etc/shadow")]
    [InlineData("double&&and")]
    [InlineData("double||or")]
    [InlineData("hash#comment")]
    [InlineData("glob*star")]
    [InlineData("tilde~expand")]
    [InlineData("unicode 日本語 emoji 🎉")]
    [InlineData("")]
    public void Build_RoundTripsAnyArgument_AsExactlyOneLiteralArgvEntry_NeverAdditionalSyntax(string maliciousArgument)
    {
        var commandLine = SshCommandLineBuilder.Build("echo", [maliciousArgument]);
        var tokens = ReferencePosixUnquote(commandLine);

        Assert.Equal(2, tokens.Count); // "echo" + exactly the one argument — never split into more tokens
        Assert.Equal("echo", tokens[0]);
        Assert.Equal(maliciousArgument, tokens[1]);
    }

    [Fact]
    public void Build_MultipleArguments_EachStaysItsOwnDiscreteToken_NeverMergedOrSplit()
    {
        var commandLine = SshCommandLineBuilder.Build("systemctl", ["show", "nginx.service; rm -rf /", "--no-pager"]);
        var tokens = ReferencePosixUnquote(commandLine);

        Assert.Equal(["systemctl", "show", "nginx.service; rm -rf /", "--no-pager"], tokens);
    }

    [Fact]
    public void Build_MaliciousExecutableName_IsAlsoQuoted_NeverExecutedAsSyntax()
    {
        var commandLine = SshCommandLineBuilder.Build("evil; rm -rf /", ["arg"]);
        var tokens = ReferencePosixUnquote(commandLine);

        Assert.Equal(["evil; rm -rf /", "arg"], tokens);
    }

    [Fact]
    public void Build_EmptyArgument_ProducesANonEmptyQuotedToken_NeverDisappearsOrMerges()
    {
        var commandLine = SshCommandLineBuilder.Build("cmd", ["", "next"]);
        var tokens = ReferencePosixUnquote(commandLine);

        Assert.Equal(["cmd", "", "next"], tokens);
    }

    [Fact]
    public void Build_NoArguments_ProducesJustTheQuotedExecutable()
    {
        var commandLine = SshCommandLineBuilder.Build("uname", []);
        Assert.Equal("'uname'", commandLine);
    }

    /// <summary>A minimal, independent reference implementation of POSIX single-quote parsing —
    /// splits on unquoted whitespace, treats a <c>'...'</c> run (with the standard
    /// <c>'\''</c> embedded-quote escape) as one literal token. Deliberately NOT shared code
    /// with <see cref="SshCommandLineBuilder"/> — it exists purely to verify that code from the
    /// opposite direction.</summary>
    private static List<string> ReferencePosixUnquote(string commandLine)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inToken = false;
        var i = 0;

        while (i < commandLine.Length)
        {
            var c = commandLine[i];

            if (c == '\'')
            {
                // Consume one quoted segment verbatim up to (and past) its matching quote.
                inToken = true;
                i++;
                while (i < commandLine.Length && commandLine[i] != '\'')
                {
                    current.Append(commandLine[i]);
                    i++;
                }

                if (i >= commandLine.Length)
                {
                    throw new InvalidOperationException("Unterminated quote in generated command line.");
                }

                i++; // consume the closing quote
            }
            else if (inToken && c == '\\' && i + 1 < commandLine.Length && commandLine[i + 1] == '\'')
            {
                // The '\'' embedded-quote escape: a literal quote between two quoted segments.
                current.Append('\'');
                i += 2;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    inToken = false;
                }
                i++;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unexpected unquoted character '{c}' at position {i} — SshCommandLineBuilder must quote everything.");
            }
        }

        if (inToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
