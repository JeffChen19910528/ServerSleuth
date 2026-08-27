namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-3 §Step3: pairs a validated, credential-free <see cref="ScanRequest"/> with the SEPARATE,
/// transient <see cref="ScanCredentialInput"/> the execution boundary needs to actually connect
/// a remote transport — never merged into one object (see <see cref="ScanCredentialInput"/>'s
/// own doc comment for why that would be dangerous). This is a one-shot, in-memory event-args
/// payload only: it is never held by <see cref="GuiApplicationState"/>, never queued, never
/// serialized — <c>ServerSleuth.Gui.ViewModels.MainViewModel</c> consumes it synchronously
/// inside its <c>ScanConfigurationViewModel.ScanRequested</c> handler and hands the credential
/// half straight to <c>IGuiScanExecutor.ExecuteAsync</c>, never storing a copy anywhere else.
/// </summary>
public sealed class ScanRequestedEventArgs : EventArgs
{
    public required ScanRequest Request { get; init; }
    public required ScanCredentialInput Credentials { get; init; }
}
