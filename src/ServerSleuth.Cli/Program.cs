using ServerSleuth.Cli;
using ServerSleuth.Cli.Composition;

using var cts = new CancellationTokenSource();

// Ctrl+C requests cancellation rather than abruptly killing the process (skill.md Phase 10A
// §18) — e.Cancel = true stops the default "terminate immediately" behavior, letting the scan
// pipeline observe the token and the exporter's own atomic-write guarantee (Phase 9C) keep any
// in-progress report file from ever being left partially written.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var app = new CliApplication(CompositionRoot.Build, Console.Out, Console.Error);
return await app.RunAsync(args, cts.Token);
