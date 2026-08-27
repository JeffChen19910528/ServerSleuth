using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Fakes;

/// <summary>GUI-3 §Step13: "use fake/in-memory execution boundaries for most tests" — never a
/// real transport, real scanner, or real report export. Deterministic: reports a fixed sequence
/// of stages and returns a caller-supplied completion (or a default success), optionally
/// honoring cancellation and optionally recording every call it received so a test can assert
/// on exactly what was passed through (target/credentials/etc.) without ever touching a real
/// resource.</summary>
internal sealed class FakeGuiScanExecutor : IGuiScanExecutor
{
    public ScanCompletionState CompletionToReturn { get; set; } = new()
    {
        Status = ScanExecutionStatus.Completed,
        EntityCount = 3,
        OutputPaths = ["report.json", "report.html"]
    };

    public List<ScanProgressState> ReportedProgress { get; } = [];
    public List<(ScanRequest Request, ScanCredentialInput Credentials)> Calls { get; } = [];
    public bool ThrowInsteadOfReturning { get; set; }
    public bool RespectCancellation { get; set; } = true;
    public TaskCompletionSource<bool>? Gate { get; set; }

    public async Task<ScanCompletionState> ExecuteAsync(
        ScanRequest request, ScanCredentialInput credentials, IProgress<ScanProgressState> progress, CancellationToken cancellationToken)
    {
        Calls.Add((request, credentials));

        var preparing = new ScanProgressState { Stage = ScanStage.Preparing };
        ReportedProgress.Add(preparing);
        progress.Report(preparing);

        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (RespectCancellation)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var discovery = new ScanProgressState { Stage = ScanStage.Discovery, EntityCount = CompletionToReturn.EntityCount };
        ReportedProgress.Add(discovery);
        progress.Report(discovery);

        if (ThrowInsteadOfReturning)
        {
            throw new InvalidOperationException("Simulated unexpected failure — never a credential-shaped message.");
        }

        return CompletionToReturn;
    }
}
