using ServerSleuth.Cli.Commands;
using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Options;
using ServerSleuth.Cli.Output;

namespace ServerSleuth.Cli;

/// <summary>
/// The CLI's testable core — <c>Program.cs</c> is a thin wrapper that constructs one of these
/// with the real console/composition root and calls <see cref="RunAsync"/>; tests construct one
/// with fakes (skill.md Phase 10A §22: "use fakes for CLI unit tests, do not make every unit
/// test depend on a real Windows/Linux machine"). Owns top-level error handling: every expected
/// failure category (§17) maps to a specific <see cref="CliExitCode"/> and a concise message,
/// never a stack trace by default.
/// </summary>
public sealed class CliApplication(Func<ScanOptions, IServiceProvider> serviceProviderFactory, TextWriter output, TextWriter error)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var console = new TextWriterConsoleWriter(output, error);

        if (args.Length == 0 || IsHelpFlag(args[0]))
        {
            console.WriteLine(HelpText.Root);
            return CliExitCode.Success;
        }

        if (IsVersionFlag(args[0]))
        {
            console.WriteLine(VersionInfo.Version);
            return CliExitCode.Success;
        }

        if (args[0] == "scan")
        {
            return await RunScanAsync(args[1..], console, cancellationToken);
        }

        console.WriteErrorLine($"Unknown command '{args[0]}'. Run 'serversleuth --help' for usage.");
        return CliExitCode.InvalidArguments;
    }

    private async Task<int> RunScanAsync(string[] scanArgs, IConsoleWriter console, CancellationToken cancellationToken)
    {
        if (scanArgs.Any(a => a is "--help" or "-h"))
        {
            console.WriteLine(HelpText.Scan);
            return CliExitCode.Success;
        }

        ScanOptions options;
        try
        {
            options = ScanOptionsParser.Parse(scanArgs);
        }
        catch (CliArgumentException ex)
        {
            console.WriteErrorLine(ex.Message);
            return CliExitCode.InvalidArguments;
        }

        IServiceProvider? services = null;
        try
        {
            services = serviceProviderFactory(options);
            return await ScanCommand.RunAsync(options, services, console, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            console.WriteErrorLine("Scan cancelled.");
            return CliExitCode.Cancelled;
        }
        catch (Exception ex)
        {
            // No stack trace by default (§17) — a concise message only. There is no verbose/
            // diagnostic flag in Phase 10A's own option surface (§6) to opt into more detail.
            console.WriteErrorLine($"Error: {ex.Message}");
            return CliExitCode.GeneralFailure;
        }
        finally
        {
            (services as IDisposable)?.Dispose();
        }
    }

    private static bool IsHelpFlag(string arg) => arg is "--help" or "-h" or "help";
    private static bool IsVersionFlag(string arg) => arg is "--version" or "-v";
}
