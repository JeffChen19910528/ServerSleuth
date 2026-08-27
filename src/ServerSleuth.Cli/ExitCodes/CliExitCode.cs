namespace ServerSleuth.Cli.ExitCodes;

/// <summary>
/// Explicit process exit codes — see skill.md (Phase 10A) §15. No exit-code semantics are
/// specified anywhere in skill.md/ARCHITECTURE.md/IMPLEMENTATION_PLAN.md/PROGRESS.md, so this is
/// this phase's own definition; document any future addition here rather than inventing a bare
/// integer at a call site.
///
/// AccessDenied/PartiallySupported/NotInstalled scanner results are never treated as a
/// <see cref="GeneralFailure"/> (skill.md §16) — they surface as <see cref="PartialDiscovery"/>
/// only, alongside a normally-produced report; the pipeline is never aborted because of them.
/// </summary>
public static class CliExitCode
{
    public const int Success = 0;

    /// <summary>An unexpected error occurred (a genuine bug, an unhandled exception from a layer
    /// this CLI composes) — never used for an expected/anticipated failure, which gets one of the
    /// more specific codes below instead.</summary>
    public const int GeneralFailure = 1;

    /// <summary>The command line itself was invalid — unknown command/option, a missing required
    /// value, or an out-of-range option value (e.g. <c>--format xml</c>).</summary>
    public const int InvalidArguments = 2;

    /// <summary>Discovery/analysis/rendering all completed, but writing the report artifact(s) to
    /// the output directory failed — an existing file under the default <c>FailIfExists</c>
    /// policy, an inaccessible/invalid output directory, or any other
    /// <c>ReportBundleExportResult.Success == false</c> outcome.</summary>
    public const int ExportFailure = 3;

    /// <summary>Discovery completed and a full report was produced, but at least one scanner
    /// reported <see cref="ServerSleuth.Core.Enums.ScannerStatus.PartiallySupported"/>,
    /// <see cref="ServerSleuth.Core.Enums.ScannerStatus.AccessDenied"/>, or
    /// <see cref="ServerSleuth.Core.Enums.ScannerStatus.Failed"/> — never fatal, only a visible signal that
    /// the report's own coverage is less than complete (mirrors Phase 8C's
    /// <c>AssessmentCoverage.Limited</c>/<c>Partial</c> trigger set; <c>NotApplicable</c>/
    /// <c>NotInstalled</c> are treated as neutral "nothing here to scan," not partial).</summary>
    public const int PartialDiscovery = 4;

    /// <summary>The user requested cancellation (Ctrl+C) before the scan finished — not one of
    /// skill.md §15's "at minimum" four codes, added because collapsing a deliberate user
    /// cancellation into <see cref="GeneralFailure"/> would make the two indistinguishable from a
    /// script/caller's perspective.</summary>
    public const int Cancelled = 5;
}
