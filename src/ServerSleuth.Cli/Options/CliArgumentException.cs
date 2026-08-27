namespace ServerSleuth.Cli.Options;

/// <summary>Thrown for an invalid command line — unknown command/option, a missing required
/// value, or an out-of-range option value. Always maps to
/// <see cref="ExitCodes.CliExitCode.InvalidArguments"/>, never to
/// <see cref="ExitCodes.CliExitCode.GeneralFailure"/> — invalid input is an expected,
/// specifically-diagnosed failure, not an unexpected error.</summary>
public sealed class CliArgumentException(string message) : Exception(message);
