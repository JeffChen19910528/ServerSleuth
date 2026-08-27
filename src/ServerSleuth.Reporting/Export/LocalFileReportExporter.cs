using System.Security.Cryptography;
using System.Text.Json;

namespace ServerSleuth.Reporting.Export;

/// <summary>
/// Writes <see cref="ReportArtifact"/>/<see cref="ReportBundle"/> content to a local filesystem
/// directory — see skill.md (Phase 9C) §5-6, §14-15. The first Reporting component allowed to
/// touch the filesystem at all: it only ever creates the caller-specified output directory and
/// writes the exact bytes an already-rendered artifact carries — never scans directories, reads
/// arbitrary files, inspects the registry/processes, executes commands, or reaches the network/
/// Docker/Kubernetes. No `DiscoveryEntity`/`RiskFinding` is ever inspected here; this type has no
/// reference to anything upstream of a already-rendered <see cref="ReportArtifact"/>.
///
/// Atomic write strategy (skill.md §6): content is written to a randomly-named temporary file in
/// the SAME output directory as the final target (required for the subsequent move to be atomic
/// on both platforms — a cross-volume move is never atomic), flushed to the OS (and, via
/// <c>FileStream.Flush(true)</c>, asked to flush through to physical storage) and closed, then
/// promoted to the final name via <see cref="File.Move(string, string, bool)"/> — on Windows this
/// uses the OS's replace-file primitive when overwriting, and on Linux <c>rename(2)</c>, both of
/// which are atomic for same-filesystem renames. The one platform-dependent limitation: neither
/// primitive is atomic across a bind-mount/network-filesystem boundary within the same output
/// directory, which is outside this implementation's control — the temp file and final file are
/// always siblings in the caller's own output directory, so this only matters if that directory
/// itself spans such a boundary.
/// </summary>
public sealed class LocalFileReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };

    public ReportExportResult Export(ReportArtifact artifact, string outputDirectory, ReportOverwritePolicy overwritePolicy = ReportOverwritePolicy.FailIfExists)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        if (!ReportFileNameValidator.IsSafe(artifact.FileName))
        {
            return Failure(artifact.Format, $"'{artifact.FileName}' is not a safe report file name — export refused.");
        }

        if (!TryEnsureDirectory(outputDirectory, out var directoryError))
        {
            return Failure(artifact.Format, directoryError!);
        }

        var finalPath = Path.Combine(outputDirectory, artifact.FileName);
        var bytes = artifact.Encoding.GetBytes(artifact.Content);

        var (success, diagnostics) = WriteAtomic(finalPath, bytes, overwritePolicy);
        if (!success)
        {
            return Failure(artifact.Format, diagnostics);
        }

        return new ReportExportResult
        {
            Success = true,
            Format = artifact.Format,
            OutputPath = finalPath,
            BytesWritten = bytes.LongLength
        };
    }

    public ReportBundleExportResult ExportBundle(
        ReportBundle bundle,
        string outputDirectory,
        ReportOverwritePolicy overwritePolicy = ReportOverwritePolicy.FailIfExists,
        bool includeManifest = false,
        DateTimeOffset? manifestCreatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var jsonResult = Export(bundle.Json, outputDirectory, overwritePolicy);
        var htmlResult = Export(bundle.Html, outputDirectory, overwritePolicy);

        ReportExportResult? manifestResult = null;
        if (includeManifest)
        {
            manifestResult = ExportManifest(bundle, jsonResult, htmlResult, outputDirectory, overwritePolicy, manifestCreatedAt);
        }

        return new ReportBundleExportResult { Json = jsonResult, Html = htmlResult, Manifest = manifestResult };
    }

    private ReportExportResult ExportManifest(
        ReportBundle bundle,
        ReportExportResult jsonResult,
        ReportExportResult htmlResult,
        string outputDirectory,
        ReportOverwritePolicy overwritePolicy,
        DateTimeOffset? createdAt)
    {
        const string manifestFileName = "report-manifest.json";

        if (!jsonResult.Success || !htmlResult.Success)
        {
            return Failure(ReportFormat.Json, "Manifest not written — one or both report artifacts failed to export.");
        }

        var manifest = new ReportManifest
        {
            Artifacts =
            [
                BuildManifestEntry(bundle.Json),
                BuildManifestEntry(bundle.Html)
            ],
            CreatedAt = createdAt
        };

        var manifestJson = JsonSerializer.Serialize(manifest, ManifestOptions);

        if (!TryEnsureDirectory(outputDirectory, out var directoryError))
        {
            return Failure(ReportFormat.Json, directoryError!);
        }

        var finalPath = Path.Combine(outputDirectory, manifestFileName);
        var bytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);

        var (success, diagnostics) = WriteAtomic(finalPath, bytes, overwritePolicy);
        if (!success)
        {
            return Failure(ReportFormat.Json, diagnostics);
        }

        return new ReportExportResult
        {
            Success = true,
            Format = ReportFormat.Json,
            OutputPath = finalPath,
            BytesWritten = bytes.LongLength
        };
    }

    /// <summary>SHA-256 over the exact bytes <see cref="Export"/> writes for this artifact —
    /// <c>Encoding.GetBytes(Content)</c>, the identical buffer used for the on-disk write, so
    /// <c>SHA256(read(file)) == entry.Sha256</c> holds by construction, not by coincidence.</summary>
    private static ReportManifestEntry BuildManifestEntry(ReportArtifact artifact)
    {
        var bytes = artifact.Encoding.GetBytes(artifact.Content);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return new ReportManifestEntry
        {
            Format = artifact.Format.ToString(),
            FileName = artifact.FileName,
            ContentLength = artifact.ContentLength,
            Sha256 = hash
        };
    }

    private static bool TryEnsureDirectory(string outputDirectory, out string? error)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PathTooLongException or NotSupportedException)
        {
            error = $"Could not create or access output directory '{outputDirectory}': {ex.Message}";
            return false;
        }
    }

    private static (bool Success, IReadOnlyList<string> Diagnostics) WriteAtomic(string finalPath, byte[] bytes, ReportOverwritePolicy policy)
    {
        if (policy == ReportOverwritePolicy.FailIfExists && File.Exists(finalPath))
        {
            return (false, [$"File already exists at '{finalPath}' and the overwrite policy is FailIfExists."]);
        }

        var tempPath = finalPath + "." + Path.GetRandomFileName() + ".tmp";

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, finalPath, overwrite: policy == ReportOverwritePolicy.Overwrite);
            return (true, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (false, [$"Failed to write '{finalPath}': {ex.Message}"]);
        }
        finally
        {
            TryDeleteIfExists(tempPath);
        }
    }

    private static void TryDeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only — never let a stray temp file mask the real export outcome.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static ReportExportResult Failure(ReportFormat format, string diagnostic) => Failure(format, [diagnostic]);

    private static ReportExportResult Failure(ReportFormat format, IReadOnlyList<string> diagnostics) => new()
    {
        Success = false,
        Format = format,
        OutputPath = null,
        BytesWritten = 0,
        Diagnostics = diagnostics
    };
}
