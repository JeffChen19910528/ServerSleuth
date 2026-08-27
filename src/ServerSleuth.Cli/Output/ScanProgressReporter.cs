using ServerSleuth.Analysis.Migration.Consolidation;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;
using ServerSleuth.Core.Targets;

namespace ServerSleuth.Cli.Output;

/// <summary>
/// Concise, human-readable scan progress — see skill.md (Phase 10A) §12. Every number printed
/// here is read directly off an already-computed pipeline artifact (<see cref="AggregateDiscoveryResult"/>,
/// <see cref="ServerRiskSummary"/>, <see cref="ServerMigrationSummary"/>) — this type formats,
/// it never counts/classifies/recomputes anything itself. Entirely suppressed when constructed
/// with <c>quiet: true</c> (only <see cref="IConsoleWriter.WriteErrorLine"/> calls elsewhere are
/// still shown — quiet mode silences progress, not errors).
///
/// Phase 10B §5-6, §13: <c>verbose</c> adds a per-scanner Id/Status/entity-count breakdown and
/// stage durations — every value still read directly off an already-computed artifact
/// (<see cref="DiscoveryResult"/>, a <see cref="TimeSpan"/> the CLI itself measured), never a
/// secret/raw-configuration value (no scanner surfaces those in its own Id/Status/count). If
/// both <c>quiet</c> and <c>verbose</c> are set, quiet wins — there is no progress output left
/// for verbose to add detail to.
/// </summary>
public sealed class ScanProgressReporter(IConsoleWriter console, bool quiet, bool verbose = false)
{
    public void WriteHeader()
    {
        if (quiet) return;
        console.WriteLine("ServerSleuth");
        console.WriteLine("============");
        console.WriteLine(string.Empty);
    }

    /// <summary>Phase 10C §14: target identity is safe, structural data (an Id/Kind/Platform
    /// this type already trusts everywhere else) — printed only in <c>verbose</c> mode, since
    /// the default view has never needed to distinguish targets (there was only ever one).
    /// Never prints anything beyond <see cref="ScanTarget.Id"/>/<see cref="ScanTarget.Platform"/>
    /// — no transport/credential/connection detail exists on the type to leak in the first place.</summary>
    public void WriteTarget(ScanTarget target)
    {
        if (quiet || !verbose) return;
        console.WriteLine($"Target: {target.Id} ({target.Platform})");
    }

    public void WriteDiscoveryStarting()
    {
        if (quiet) return;
        console.WriteLine("Discovering server...");
    }

    public void WriteDiscoveryComplete(AggregateDiscoveryResult discovery, TimeSpan elapsed)
    {
        if (quiet) return;

        var partial = discovery.ScannerStatuses.Values.Count(IsPartialStatus);

        console.WriteLine(string.Empty);
        console.WriteLine("Discovery complete");
        console.WriteLine($"  Entities: {discovery.Entities.Count}");
        console.WriteLine($"  Scanners: {discovery.ScannerResults.Count}");
        console.WriteLine($"  Partial:  {partial}");

        if (verbose)
        {
            console.WriteLine($"  Duration: {FormatDuration(elapsed)}");
        }

        console.WriteLine(string.Empty);

        if (verbose)
        {
            WriteScannerBreakdown(discovery);
        }
    }

    /// <summary>Phase 10B §6: one line per registered scanner, in the same registry order
    /// <see cref="AggregateDiscoveryResult.ScannerResults"/> already carries — Id/Status/entity
    /// count are the exact values that scanner's own <see cref="DiscoveryResult"/> returned;
    /// nothing here is fabricated, reclassified, or reordered.</summary>
    private void WriteScannerBreakdown(AggregateDiscoveryResult discovery)
    {
        console.WriteLine("Scanning:");
        foreach (var result in discovery.ScannerResults)
        {
            console.WriteLine($"  {result.ScannerId,-32} {result.Status,-18} {result.Entities.Count}");
        }

        console.WriteLine(string.Empty);
    }

    public void WriteAnalyzing()
    {
        if (quiet) return;
        console.WriteLine("Analyzing dependencies...");
    }

    public void WriteAnalysisComplete(TimeSpan elapsed)
    {
        if (quiet) return;
        console.WriteLine("Analysis complete");
        if (verbose)
        {
            console.WriteLine($"  Duration: {FormatDuration(elapsed)}");
        }

        console.WriteLine(string.Empty);
    }

    public void WriteMigrationAssessmentComplete()
    {
        if (quiet) return;
        console.WriteLine("Migration assessment complete");
        console.WriteLine(string.Empty);
    }

    public void WriteRiskSummary(ServerRiskSummary risk)
    {
        if (quiet) return;
        console.WriteLine("Risk:");
        console.WriteLine($"  Critical: {risk.CriticalCount}");
        console.WriteLine($"  High:     {risk.HighCount}");
        console.WriteLine($"  Medium:   {risk.MediumCount}");
        console.WriteLine(string.Empty);
    }

    public void WriteMigrationSummary(ServerMigrationSummary summary)
    {
        if (quiet) return;
        console.WriteLine("Migration:");
        console.WriteLine($"  Blocked:             {summary.BlockedApplicationCount}");
        console.WriteLine($"  NeedsRemediation:    {summary.NeedsRemediationApplicationCount}");
        console.WriteLine($"  ReadyWithConditions: {summary.ReadyWithConditionsApplicationCount}");
        console.WriteLine(string.Empty);
    }

    public void WriteWritingReports()
    {
        if (quiet) return;
        console.WriteLine("Writing reports...");
    }

    public void WriteReportWritten(string fileName)
    {
        if (quiet) return;
        console.WriteLine($"  {fileName}");
    }

    public void WriteCompleted()
    {
        if (quiet) return;
        console.WriteLine(string.Empty);
        console.WriteLine("Completed.");
    }

    private static string FormatDuration(TimeSpan elapsed) => $"{elapsed.TotalSeconds:0.00}s";

    /// <summary>Mirrors Phase 8C's own <c>AssessmentCoverage</c> trigger set (skill.md §16,
    /// Phase 10A §15's PartialDiscovery exit code): PartiallySupported/AccessDenied/Failed count
    /// as "partial" — NotApplicable/NotInstalled are a neutral "nothing here to scan," not a gap.</summary>
    internal static bool IsPartialStatus(ScannerStatus status) =>
        status is ScannerStatus.PartiallySupported or ScannerStatus.AccessDenied or ScannerStatus.Failed;
}
