namespace ServerSleuth.Gui.Models;

/// <summary>GUI-3 §Step11: the overall status of a scan execution — distinct from
/// <see cref="ScanStage"/> (WHICH stage is running) in that this only ever changes at the
/// beginning and the very end of a run.</summary>
public enum ScanExecutionStatus
{
    Idle,
    Preparing,
    Running,
    Completed,
    Partial,
    Cancelled,
    Failed
}
