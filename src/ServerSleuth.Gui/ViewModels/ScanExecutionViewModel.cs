using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Navigation;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// GUI-3's Scan Execution ViewModel — the real ViewModel shown once
/// <see cref="ScanConfigurationViewModel.ScanRequested"/> fires. Owns only the non-sensitive
/// <see cref="ScanExecutionState"/> (freely bindable); credentials passed into
/// <see cref="Start"/> are handed straight to <see cref="IGuiScanExecutor.ExecuteAsync"/> and
/// never stored on this type as a field (mechanically verified alongside
/// <c>ScanConfigurationViewModel</c>'s own equivalent guarantee).
///
/// Never instantiates <c>DiscoveryEngine</c>/<c>ScanPipelineRunner</c>/a scanner/a transport
/// itself — the ONLY dependency this class has is the abstract <see cref="IGuiScanExecutor"/>
/// (mechanically verified by <c>NoScanExecutionFromGuiTests</c>, extended for this type in
/// Phase GUI-3).
/// </summary>
public sealed class ScanExecutionViewModel : ObservableObject, IPageViewModel
{
    private readonly IGuiScanExecutor _executor;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;

    public ScanExecutionViewModel(IGuiScanExecutor executor)
    {
        _executor = executor;

        CancelCommand = new RelayCommand(_ => Cancel(), _ => _isRunning);
        NewScanCommand = new RelayCommand(_ => ReturnToConfigurationRequested?.Invoke(this, EventArgs.Empty), _ => !_isRunning);
        ViewResultsCommand = new RelayCommand(_ => ViewResultsRequested?.Invoke(this, EventArgs.Empty), _ => IsFinished);
    }

    public NavigationPage Page => NavigationPage.Scan;

    /// <summary>Raised when the user asks to go back to Scan Configuration — "New Scan," never
    /// raised while a scan is running. GUI-4 §Step4 keeps this exactly as GUI-3 left it (returns
    /// to configuration) alongside the new <see cref="ViewResultsRequested"/> — GUI-4 supplements
    /// this completion flow, it does not replace it.</summary>
    public event EventHandler? ReturnToConfigurationRequested;

    /// <summary>GUI-4 §Step4: "View Results" — raised only once a scan has actually finished
    /// (<see cref="IsFinished"/>), never while running and never before any scan has started.
    /// Carries no data itself; the listener (<see cref="MainViewModel"/>) reads the already-built
    /// <see cref="State"/> off this same ViewModel to build the Results Dashboard — no second
    /// execution, no re-fetch.</summary>
    public event EventHandler? ViewResultsRequested;

    private ScanExecutionState _state = ScanExecutionState.Idle;
    public ScanExecutionState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsFinished));
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool IsRunning => State.Status is ScanExecutionStatus.Preparing or ScanExecutionStatus.Running;

    public bool IsFinished => State.Status is ScanExecutionStatus.Completed or ScanExecutionStatus.Partial
        or ScanExecutionStatus.Cancelled or ScanExecutionStatus.Failed;

    public bool HasErrorMessage => !string.IsNullOrEmpty(State.ErrorMessage);

    public RelayCommand CancelCommand { get; }
    public RelayCommand NewScanCommand { get; }
    public RelayCommand ViewResultsCommand { get; }

    /// <summary>skill.md GUI-3 §Step9: only one scan may execute at a time — a second
    /// <see cref="Start"/> call while one is already running is silently ignored (deterministic,
    /// never throws, never queues a second run).</summary>
    public void Start(ScanRequest request, ScanCredentialInput credentials)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;

        _cancellationTokenSource = new CancellationTokenSource();
        State = ScanExecutionState.StartingFor(request.Target, request.OutputDirectory);

        // Progress<T> marshals each report via whichever SynchronizationContext was active when
        // this Progress<T> was constructed (the WPF Dispatcher's, in the real app) — a report
        // queued just before completion can therefore still be DELIVERED after the terminal
        // State.WithCompletion(...) write below has already run. Guard against that: once this
        // ViewModel has reached a terminal state, a late-arriving progress report must never
        // regress it back to "running."
        var progress = new Progress<ScanProgressState>(p =>
        {
            if (!IsFinished)
            {
                State = State.WithProgress(p);
            }
        });
        _ = RunAsync(request, credentials, progress, _cancellationTokenSource.Token);
    }

    private async Task RunAsync(ScanRequest request, ScanCredentialInput credentials, IProgress<ScanProgressState> progress, CancellationToken cancellationToken)
    {
        ScanCompletionState completion;
        try
        {
            // Most IDiscoveryScanner implementations do their Registry/WMI/filesystem/IIS/COM
            // work synchronously and only wrap the result in Task.FromResult — there is no real
            // await anywhere in that chain to yield control back to the WPF message pump. Left
            // un-offloaded, the entire scan (which can run for minutes under ScanProfile.Migration)
            // would execute directly on this UI-thread-originated async continuation, freezing the
            // window ("Not Responding") and starving the Progress<T> reports that are supposed to
            // animate the progress UI. Task.Run moves the whole executor call to the thread pool;
            // Progress<T> still marshals reports back via the SynchronizationContext it captured
            // when constructed on the UI thread in Start(), so progress updates keep working.
            completion = await Task.Run(
                () => _executor.ExecuteAsync(request, credentials, progress, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            completion = ScanCompletionState.Cancelled();
        }
        catch (Exception)
        {
            // skill.md §10: never a raw exception message here — IGuiScanExecutor
            // implementations are already expected to convert their own internal failures into
            // a generic ScanCompletionState.Failed(...); this catch is the ViewModel's own
            // last-resort safety net for a defect in an implementation that didn't.
            completion = ScanCompletionState.Failed("An unexpected error occurred during the scan. See application logs for details.");
        }

        State = State.WithCompletion(completion);
        _isRunning = false;
    }

    /// <summary>skill.md GUI-3 §Step8: idempotent — safe to call any number of times, including
    /// after the scan has already finished (a no-op once <see cref="_cancellationTokenSource"/>
    /// is <c>null</c> or already cancelled). Never touches a process directly
    /// (<c>Process.Kill</c>) — cancellation flows purely through the existing
    /// <see cref="CancellationToken"/> architecture the pipeline/transports already honor.</summary>
    private void Cancel() => _cancellationTokenSource?.Cancel();
}
