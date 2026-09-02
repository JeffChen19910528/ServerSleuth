#requires -Version 5.1
<#
.SYNOPSIS
    Phase 11B v1.0.0 release packaging: publishes self-contained, single-file release
    artifacts for Windows x64 (GUI + CLI) and Linux x64 (CLI), and assembles the final
    portable distribution packages.

.DESCRIPTION
    This script is separate from normal development builds (`dotnet build` / `dotnet test`)
    — it does not change how those behave. It publishes three already-existing entry points:

      Windows x64 GUI: src/ServerSleuth.Gui  (net8.0-windows) -> ServerSleuth.exe
      Windows x64 CLI: src/ServerSleuth.Cli  (net8.0-windows) -> serversleuth-cli.exe
      Linux   x64 CLI: src/ServerSleuth.Cli  (net8.0)          -> serversleuth

    Output goes to release/windows/, release/linux/, plus compressed distribution packages
    (ServerSleuth-v<version>-windows-x64.zip / .tar.gz) and a top-level release/VERSION and
    release/SHA256SUMS.txt covering every raw executable AND both archives.

    The product version is read from the repository's own Directory.Build.props (single
    source of truth — see that file) rather than hard-coded here, so this script can never
    silently drift out of sync with what every assembly actually reports.

.USAGE
    .\build-release.ps1
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$releaseRoot = Join-Path $repoRoot 'release'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Fail {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Format-Bytes {
    param([long]$Bytes)
    "{0:N2} MB" -f ($Bytes / 1MB)
}

# ---------------------------------------------------------------------------
# 1. Validate prerequisites
# ---------------------------------------------------------------------------
Write-Step "Validating prerequisites"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Fail ".NET SDK ('dotnet') was not found on PATH."
}

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion)) {
    Fail "Could not determine the installed .NET SDK version."
}
Write-Host "  dotnet SDK: $sdkVersion"

$guiProject = Join-Path $repoRoot 'src\ServerSleuth.Gui\ServerSleuth.Gui.csproj'
$cliProject = Join-Path $repoRoot 'src\ServerSleuth.Cli\ServerSleuth.Cli.csproj'
$buildPropsPath = Join-Path $repoRoot 'Directory.Build.props'
if (-not (Test-Path $guiProject)) { Fail "GUI project not found at $guiProject" }
if (-not (Test-Path $cliProject)) { Fail "CLI project not found at $cliProject" }
if (-not (Test-Path $buildPropsPath)) { Fail "Directory.Build.props not found at $buildPropsPath" }
Write-Host "  GUI project: $guiProject"
Write-Host "  CLI project: $cliProject"

# Read the version straight out of Directory.Build.props — never a second, hard-coded copy
# that could silently drift from what every published assembly actually reports.
$buildPropsXml = [xml](Get-Content $buildPropsPath -Raw)
$version = $buildPropsXml.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    Fail "Could not read <Version> from Directory.Build.props."
}
Write-Host "  Release version: $version"

$tarCmd = Get-Command tar -ErrorAction SilentlyContinue
if (-not $tarCmd) {
    Fail "'tar' was not found on PATH (needed to produce the Linux .tar.gz archive — ships built into Windows 10 1803+/Windows 11)."
}

# ---------------------------------------------------------------------------
# 2. Clean the release output directory
# ---------------------------------------------------------------------------
Write-Step "Cleaning release output directory"

if (Test-Path $releaseRoot) {
    Remove-Item -Recurse -Force $releaseRoot
}
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
Write-Host "  Cleaned: $releaseRoot"

$winOutDir = Join-Path $releaseRoot 'windows'
$linuxOutDir = Join-Path $releaseRoot 'linux'
New-Item -ItemType Directory -Force -Path $winOutDir | Out-Null
New-Item -ItemType Directory -Force -Path $linuxOutDir | Out-Null

# Intermediate publish staging directories, kept OUT of release/ so release/ only ever
# contains the final, renamed release artifacts.
$publishStagingRoot = Join-Path $repoRoot 'obj\release-publish-staging'
if (Test-Path $publishStagingRoot) {
    Remove-Item -Recurse -Force $publishStagingRoot
}
$winGuiStagingDir = Join-Path $publishStagingRoot 'win-x64-gui'
$winCliStagingDir = Join-Path $publishStagingRoot 'win-x64-cli'
$linuxStagingDir = Join-Path $publishStagingRoot 'linux-x64'

# ---------------------------------------------------------------------------
# 3. Publish Windows x64 GUI
# ---------------------------------------------------------------------------
Write-Step "Publishing Windows x64 GUI (ServerSleuth.Gui)"

& dotnet publish $guiProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $winGuiStagingDir

if ($LASTEXITCODE -ne 0) {
    Fail "Windows GUI publish failed (dotnet publish exited with code $LASTEXITCODE)."
}

# ---------------------------------------------------------------------------
# 4. Publish Windows x64 CLI (Phase 11B: the artifact Phase 11A found missing)
# ---------------------------------------------------------------------------
Write-Step "Publishing Windows x64 CLI (ServerSleuth.Cli, net8.0-windows)"

& dotnet publish $cliProject `
    -c $Configuration `
    -f net8.0-windows `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $winCliStagingDir

if ($LASTEXITCODE -ne 0) {
    Fail "Windows CLI publish failed (dotnet publish exited with code $LASTEXITCODE)."
}

# ---------------------------------------------------------------------------
# 5. Publish Linux x64 CLI
# ---------------------------------------------------------------------------
Write-Step "Publishing Linux x64 CLI (ServerSleuth.Cli, net8.0)"

& dotnet publish $cliProject `
    -c $Configuration `
    -f net8.0 `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $linuxStagingDir

if ($LASTEXITCODE -ne 0) {
    Fail "Linux CLI publish failed (dotnet publish exited with code $LASTEXITCODE)."
}

# ---------------------------------------------------------------------------
# 6. Assemble final release/ layout — copy ONLY the real executable out of each staging
#    directory (never the whole publish output), so .pdb/.deps.json/.runtimeconfig.json
#    (harmless leftover files `dotnet publish` still writes alongside a single-file
#    executable, none of which the executable itself needs to run) never reach release/.
# ---------------------------------------------------------------------------
Write-Step "Assembling release artifacts"

$winGuiPublishedExe = Join-Path $winGuiStagingDir 'ServerSleuth.Gui.exe'
$winCliPublishedExe = Join-Path $winCliStagingDir 'serversleuth.exe'
$linuxPublishedExe = Join-Path $linuxStagingDir 'serversleuth'

if (-not (Test-Path $winGuiPublishedExe)) { Fail "Expected published Windows GUI binary not found: $winGuiPublishedExe" }
if (-not (Test-Path $winCliPublishedExe)) { Fail "Expected published Windows CLI binary not found: $winCliPublishedExe" }
if (-not (Test-Path $linuxPublishedExe)) { Fail "Expected published Linux CLI binary not found: $linuxPublishedExe" }

# Renaming to the user-facing names here is a publish-output rename only — it does not
# touch any project's real AssemblyName (overriding AssemblyName via -p: was tried and
# rejected in Phase 11A: it propagates to every ProjectReference in the graph and produces
# an "Ambiguous project name" restore error).
Copy-Item -Path $winGuiPublishedExe -Destination (Join-Path $winOutDir 'ServerSleuth.exe')
Copy-Item -Path $winCliPublishedExe -Destination (Join-Path $winOutDir 'serversleuth-cli.exe')
Copy-Item -Path $linuxPublishedExe -Destination (Join-Path $linuxOutDir 'serversleuth')

$winGuiExe = Join-Path $winOutDir 'ServerSleuth.exe'
$winCliExe = Join-Path $winOutDir 'serversleuth-cli.exe'
$linuxExe = Join-Path $linuxOutDir 'serversleuth'

foreach ($f in @($winGuiExe, $winCliExe, $linuxExe)) {
    if (-not (Test-Path $f) -or (Get-Item $f).Length -le 0) {
        Fail "Expected release artifact missing or empty: $f"
    }
}

Write-Host "  Windows GUI: $winGuiExe ($(Format-Bytes (Get-Item $winGuiExe).Length))"
Write-Host "  Windows CLI: $winCliExe ($(Format-Bytes (Get-Item $winCliExe).Length))"
Write-Host "  Linux CLI:   $linuxExe ($(Format-Bytes (Get-Item $linuxExe).Length))"

# ---------------------------------------------------------------------------
# 7. Verify published executable versions agree with Directory.Build.props — a mismatch here
#    would mean the release archive's own name/VERSION/README disagree with what the shipped
#    binary actually reports, which must fail the build rather than publish silently.
#
#    Windows binaries only (the Linux binary is linux-x64 and cannot run on this host — its own
#    version consistency is verified by build-release.sh, which DOES run on a host that can
#    execute it). Read via FileVersionInfo (no execution) for both; additionally invoke the CLI
#    with --version (it is a plain console app — safe to run) for the strongest possible check,
#    matching exactly what a user running "serversleuth-cli.exe --version" would see. The GUI is
#    deliberately never executed here — launching a WPF window from an unattended release script
#    to check a version string is the "fragile logic" this step exists to avoid, not to add.
# ---------------------------------------------------------------------------
Write-Step "Verifying published executable versions"

# InformationalVersion (ProductVersion) is <Version> PLUS a "+<git-commit-sha>" SemVer build-
# metadata suffix the .NET SDK appends automatically from source-control info when available
# (confirmed by running this exact step: "1.0.0+3a6cb48e...") — legitimate, deterministic-build
# SDK behavior, not a defect. Per SemVer, build metadata (after "+") is ignored when comparing
# versions, so only the part before "+" (if any) is compared here.
foreach ($pair in @(@{ Name = 'Windows GUI'; Path = $winGuiExe }, @{ Name = 'Windows CLI'; Path = $winCliExe })) {
    $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($pair.Path).ProductVersion
    $productVersionCore = $productVersion.Split('+')[0]
    if ($productVersionCore -ne $version) {
        Fail "$($pair.Name) ProductVersion '$productVersion' does not match Directory.Build.props Version '$version' ($($pair.Path))."
    }
    Write-Host "  $($pair.Name) ProductVersion: $productVersion (matches)"
}

$cliVersionOutput = (& $winCliExe --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    Fail "Windows CLI '--version' invocation failed (exit code $LASTEXITCODE)."
}
# CLI --version prints the 4-part AssemblyVersion (e.g. "1.0.0.0") — a prefix match against the
# 3-part product Version is the correct comparison, not exact equality (see comment above).
if ($cliVersionOutput -ne $version -and -not $cliVersionOutput.StartsWith("$version.")) {
    Fail "Windows CLI '--version' output '$cliVersionOutput' is not consistent with Directory.Build.props Version '$version'."
}
Write-Host "  Windows CLI --version: $cliVersionOutput (consistent with $version)"

# ---------------------------------------------------------------------------
# 8. VERSION file
# ---------------------------------------------------------------------------
Write-Step "Writing VERSION"

$versionFile = Join-Path $releaseRoot 'VERSION'
Set-Content -Path $versionFile -Value $version -Encoding ASCII -NoNewline
Write-Host "  Written: $versionFile ($version)"

# ---------------------------------------------------------------------------
# 9. Per-platform README.txt (bundled inside each archive — end-user facing only, never
#    development documentation).
# ---------------------------------------------------------------------------
Write-Step "Writing per-platform README.txt"

$commonNotes = @"
ServerSleuth is a strictly READ-ONLY server discovery and migration assessment
tool. It never stops/restarts services, modifies the registry/IIS/systemd,
deletes files, installs packages, or changes firewall rules on the target it
scans. Secret-shaped values (passwords, connection strings, API keys, tokens,
private keys) are always redacted from every report.

Some scanners may show as PartiallySupported / NotInstalled / AccessDenied on
a given target machine — this reflects that machine's own configuration and
the permissions the scan was run with, not a defect in ServerSleuth itself.
"@

$winReadme = @"
ServerSleuth v$version - Windows x64
=====================================

This is a self-contained, single-file distribution. No separate .NET
installation is required.

Desktop GUI:
    Run ServerSleuth.exe

Command-line interface:
    serversleuth-cli.exe --help
    serversleuth-cli.exe --version
    serversleuth-cli.exe scan --output <output-directory>

$commonNotes
"@

$linuxReadme = @"
ServerSleuth v$version - Linux x64
====================================

This is a self-contained, single-file distribution. No separate .NET
installation is required.

    chmod +x serversleuth
    ./serversleuth --help
    ./serversleuth --version
    ./serversleuth scan --output <output-directory>

$commonNotes
"@

Set-Content -Path (Join-Path $winOutDir 'README.txt') -Value $winReadme -Encoding UTF8
Set-Content -Path (Join-Path $linuxOutDir 'README.txt') -Value $linuxReadme -Encoding UTF8
Copy-Item -Path $versionFile -Destination (Join-Path $winOutDir 'VERSION')
Copy-Item -Path $versionFile -Destination (Join-Path $linuxOutDir 'VERSION')
Write-Host "  Written: $(Join-Path $winOutDir 'README.txt'), $(Join-Path $linuxOutDir 'README.txt')"

# ---------------------------------------------------------------------------
# 10. Compressed distribution packages
# ---------------------------------------------------------------------------
Write-Step "Building compressed distribution packages"

$winZipName = "ServerSleuth-v$version-windows-x64.zip"
$linuxTarName = "ServerSleuth-v$version-linux-x64.tar.gz"
$winZipPath = Join-Path $releaseRoot $winZipName
$linuxTarPath = Join-Path $releaseRoot $linuxTarName

if (Test-Path $winZipPath) { Remove-Item -Force $winZipPath }
Compress-Archive -Path (Join-Path $winOutDir '*') -DestinationPath $winZipPath -CompressionLevel Optimal

if (Test-Path $linuxTarPath) { Remove-Item -Force $linuxTarPath }
# tar's -C changes directory before archiving so the tar contains bare filenames
# (serversleuth, README.txt, VERSION), never an absolute-path-rooted entry.
& tar -czf $linuxTarPath -C $linuxOutDir .
if ($LASTEXITCODE -ne 0) {
    Fail "Failed to create $linuxTarPath (tar exited with code $LASTEXITCODE)."
}

Write-Host "  Windows ZIP: $winZipPath ($(Format-Bytes (Get-Item $winZipPath).Length))"
Write-Host "  Linux tar.gz: $linuxTarPath ($(Format-Bytes (Get-Item $linuxTarPath).Length))"

# ---------------------------------------------------------------------------
# 11. Package content audit — fail the build if a forbidden development artifact made
#     it into either archive (this only re-checks what step 6 already guaranteed by
#     construction, as a second, independent line of defense).
# ---------------------------------------------------------------------------
Write-Step "Auditing package contents"

# 'bin'/'obj' alone only match an entry whose FullName is exactly that literal string —
# a zip entry like "bin/foo.dll" would NOT match via plain -like 'bin'. The 'bin/*'/'obj/*'
# patterns catch that nested case; the bare 'bin'/'obj' entries are kept too in case a zip
# ever contains a directory entry with no trailing content.
$forbiddenPatterns = @('*.pdb', '*.xml', '*.deps.json', '*.runtimeconfig.json', '*.csproj', '*.cs', 'bin', 'obj', 'bin/*', 'obj/*')

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($winZipPath)
try {
    $zipEntries = $zip.Entries | ForEach-Object { $_.FullName }
} finally {
    $zip.Dispose()
}

$violations = @()
foreach ($entry in $zipEntries) {
    foreach ($pattern in $forbiddenPatterns) {
        if ($entry -like $pattern) { $violations += "windows zip: $entry" }
    }
}

$auditStagingDir = Join-Path $publishStagingRoot 'tar-audit'
if (Test-Path $auditStagingDir) { Remove-Item -Recurse -Force $auditStagingDir }
New-Item -ItemType Directory -Force -Path $auditStagingDir | Out-Null
& tar -xzf $linuxTarPath -C $auditStagingDir
# Relative path with forward slashes (matching the zip entries' own separator) — not just the
# leaf .Name — so a nested "bin/foo.dll"/"obj/foo.dll" is actually checked against the
# 'bin/*'/'obj/*' patterns below; .Name alone would only ever equal "foo.dll" and could never
# match those patterns regardless of which directory the file was extracted from.
$tarEntries = Get-ChildItem -Path $auditStagingDir -Recurse -File | ForEach-Object {
    $_.FullName.Substring($auditStagingDir.Length + 1).Replace('\', '/')
}
foreach ($entry in $tarEntries) {
    foreach ($pattern in $forbiddenPatterns) {
        if ($entry -like $pattern) { $violations += "linux tar.gz: $entry" }
    }
}
Remove-Item -Recurse -Force $auditStagingDir

if ($violations.Count -gt 0) {
    Write-Host "  Contents (windows zip): $($zipEntries -join ', ')"
    Write-Host "  Contents (linux tar.gz): $($tarEntries -join ', ')"
    Fail "Package content audit found forbidden development artifact(s): $($violations -join '; ')"
}

Write-Host "  Windows ZIP contents:   $($zipEntries -join ', ')"
Write-Host "  Linux tar.gz contents:  $($tarEntries -join ', ')"
Write-Host "  No forbidden development artifacts found in either package." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 12. Checksums — every raw executable AND both archives, recomputed fresh every run.
# ---------------------------------------------------------------------------
Write-Step "Generating SHA-256 checksums"

$checksumFile = Join-Path $releaseRoot 'SHA256SUMS.txt'
$lines = @()
$lines += "$((Get-FileHash -Algorithm SHA256 $winGuiExe).Hash.ToLowerInvariant())  windows/ServerSleuth.exe"
$lines += "$((Get-FileHash -Algorithm SHA256 $winCliExe).Hash.ToLowerInvariant())  windows/serversleuth-cli.exe"
$lines += "$((Get-FileHash -Algorithm SHA256 $linuxExe).Hash.ToLowerInvariant())  linux/serversleuth"
$lines += "$((Get-FileHash -Algorithm SHA256 $winZipPath).Hash.ToLowerInvariant())  $winZipName"
$lines += "$((Get-FileHash -Algorithm SHA256 $linuxTarPath).Hash.ToLowerInvariant())  $linuxTarName"
Set-Content -Path $checksumFile -Value $lines -Encoding ASCII
Write-Host "  Written: $checksumFile"

# ---------------------------------------------------------------------------
# 13. Verify the checksums just written actually match the artifacts on disk — recomputes each
#     hash independently (never trusts $lines above) and compares against what was parsed back
#     out of the file, so a checksum-writing bug can never silently produce a green release.
# ---------------------------------------------------------------------------
Write-Step "Verifying SHA-256 checksums"

$checksumEntries = Get-Content $checksumFile | Where-Object { $_ -match '^\s*([0-9a-f]{64})\s+(.+)$' } | ForEach-Object {
    [pscustomobject]@{ Hash = $Matches[1]; RelativePath = $Matches[2] }
}
if ($checksumEntries.Count -ne $lines.Count) {
    Fail "SHA256SUMS.txt does not contain the expected $($lines.Count) checksum lines after being written."
}

foreach ($entry in $checksumEntries) {
    $artifactPath = Join-Path $releaseRoot $entry.RelativePath
    if (-not (Test-Path $artifactPath)) {
        Fail "Checksum entry '$($entry.RelativePath)' has no corresponding file at $artifactPath."
    }
    $actualHash = (Get-FileHash -Algorithm SHA256 $artifactPath).Hash.ToLowerInvariant()
    if ($actualHash -ne $entry.Hash) {
        Fail "SHA-256 mismatch for $($entry.RelativePath): SHA256SUMS.txt says $($entry.Hash), actual file hash is $actualHash."
    }
    Write-Host "  Verified: $($entry.RelativePath)"
}
Write-Host "  All $($checksumEntries.Count) checksums verified." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 14. Verify version consistency once more across every artifact this run produced — the VERSION
#     file, both archive filenames, and both per-platform README.txt files must all agree with
#     Directory.Build.props' own $version (step 7 already verified the two Windows executables
#     themselves). Everything below is guaranteed by construction (every value is interpolated
#     from the same $version variable) — this step exists so a future edit that breaks that
#     guarantee fails the build immediately instead of silently publishing inconsistent artifacts.
# ---------------------------------------------------------------------------
Write-Step "Verifying version consistency across release artifacts"

$writtenVersion = (Get-Content $versionFile -Raw).Trim()
if ($writtenVersion -ne $version) {
    Fail "release/VERSION contains '$writtenVersion', expected '$version'."
}
if ($winZipName -notlike "*v$version*") {
    Fail "Windows archive filename '$winZipName' does not contain version '$version'."
}
if ($linuxTarName -notlike "*v$version*") {
    Fail "Linux archive filename '$linuxTarName' does not contain version '$version'."
}
foreach ($readme in @((Join-Path $winOutDir 'README.txt'), (Join-Path $linuxOutDir 'README.txt'))) {
    if ((Get-Content $readme -Raw) -notlike "*v$version*") {
        Fail "$readme does not mention version '$version'."
    }
}
Write-Host "  VERSION, archive filenames, and both README.txt files all agree on $version." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Step "Release build complete — v$version"
Get-Content $checksumFile | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host "  $winGuiExe"
Write-Host "  $winCliExe"
Write-Host "  $linuxExe"
Write-Host "  $winZipPath"
Write-Host "  $linuxTarPath"
Write-Host "  $versionFile"
Write-Host "  $checksumFile"
exit 0
