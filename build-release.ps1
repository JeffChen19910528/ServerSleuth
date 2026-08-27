#requires -Version 5.1
<#
.SYNOPSIS
    Phase 11A release packaging: publishes self-contained, single-file release
    artifacts for Windows x64 (the WPF GUI) and Linux x64 (the CLI).

.DESCRIPTION
    This script is separate from normal development builds (`dotnet build` /
    `dotnet test`) — it does not change how those behave. It only publishes
    two already-existing entry points:

      Windows x64: src/ServerSleuth.Gui  (net8.0-windows) -> ServerSleuth.exe
      Linux   x64: src/ServerSleuth.Cli  (net8.0)          -> ServerSleuth

    Output goes to dist/ServerSleuth-Windows-x64/ and dist/ServerSleuth-Linux-x64/.
    SHA-256 checksums are written to dist/SHA256SUMS.txt.

.USAGE
    .\build-release.ps1
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$distRoot = Join-Path $repoRoot 'dist'

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
if (-not (Test-Path $guiProject)) { Fail "GUI project not found at $guiProject" }
if (-not (Test-Path $cliProject)) { Fail "CLI project not found at $cliProject" }
Write-Host "  GUI project: $guiProject"
Write-Host "  CLI project: $cliProject"

# ---------------------------------------------------------------------------
# 2. Clean the release output directory
# ---------------------------------------------------------------------------
Write-Step "Cleaning release output directory"

if (Test-Path $distRoot) {
    Remove-Item -Recurse -Force $distRoot
}
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
Write-Host "  Cleaned: $distRoot"

$winOutDir = Join-Path $distRoot 'ServerSleuth-Windows-x64'
$linuxOutDir = Join-Path $distRoot 'ServerSleuth-Linux-x64'

# Intermediate publish staging directories, kept OUT of dist/ so dist/ only ever
# contains the final, renamed release artifacts (skill.md/spec §4: "Do not mix
# intermediate build files with release artifacts").
$publishStagingRoot = Join-Path $repoRoot 'obj\release-publish-staging'
if (Test-Path $publishStagingRoot) {
    Remove-Item -Recurse -Force $publishStagingRoot
}
$winStagingDir = Join-Path $publishStagingRoot 'win-x64'
$linuxStagingDir = Join-Path $publishStagingRoot 'linux-x64'

# ---------------------------------------------------------------------------
# 3. Publish Windows x64 (WPF GUI)
# ---------------------------------------------------------------------------
Write-Step "Publishing Windows x64 (ServerSleuth.Gui)"

& dotnet publish $guiProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $winStagingDir

if ($LASTEXITCODE -ne 0) {
    Fail "Windows publish failed (dotnet publish exited with code $LASTEXITCODE)."
}

# ---------------------------------------------------------------------------
# 4. Publish Linux x64 (CLI)
# ---------------------------------------------------------------------------
Write-Step "Publishing Linux x64 (ServerSleuth.Cli)"

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
    Fail "Linux publish failed (dotnet publish exited with code $LASTEXITCODE)."
}

# ---------------------------------------------------------------------------
# 5. Assemble final dist/ layout
# ---------------------------------------------------------------------------
Write-Step "Assembling release artifacts"

New-Item -ItemType Directory -Force -Path $winOutDir | Out-Null
New-Item -ItemType Directory -Force -Path $linuxOutDir | Out-Null

# Published executable names come from each project's own <AssemblyName> — the GUI
# publishes as ServerSleuth.Gui.exe, the CLI as serversleuth (both lowercase-first on
# Linux). Renaming to the user-facing "ServerSleuth"/"ServerSleuth.exe" here is a
# publish-output rename only — it does not touch either project's real AssemblyName
# (overriding AssemblyName via -p: was tried and rejected: it propagates to every
# ProjectReference in the graph and produces an "Ambiguous project name" restore error).
$winPublishedExe = Join-Path $winStagingDir 'ServerSleuth.Gui.exe'
$linuxPublishedExe = Join-Path $linuxStagingDir 'serversleuth'

if (-not (Test-Path $winPublishedExe)) {
    Fail "Expected published Windows binary not found: $winPublishedExe"
}
if (-not (Test-Path $linuxPublishedExe)) {
    Fail "Expected published Linux binary not found: $linuxPublishedExe"
}

Copy-Item -Path $winPublishedExe -Destination (Join-Path $winOutDir 'ServerSleuth.exe')
Copy-Item -Path $linuxPublishedExe -Destination (Join-Path $linuxOutDir 'ServerSleuth')

$winExe = Join-Path $winOutDir 'ServerSleuth.exe'
$linuxExe = Join-Path $linuxOutDir 'ServerSleuth'

if (-not (Test-Path $winExe)) {
    Fail "Expected Windows artifact not found: $winExe"
}
if (-not (Test-Path $linuxExe)) {
    Fail "Expected Linux artifact not found: $linuxExe"
}

# ---------------------------------------------------------------------------
# 6. Verify artifacts (non-empty, report remaining files if any)
# ---------------------------------------------------------------------------
Write-Step "Verifying artifacts"

$winFiles = Get-ChildItem -Path $winOutDir -File
$linuxFiles = Get-ChildItem -Path $linuxOutDir -File

$winSize = (Get-Item $winExe).Length
$linuxSize = (Get-Item $linuxExe).Length

if ($winSize -le 0) { Fail "Windows artifact is empty: $winExe" }
if ($linuxSize -le 0) { Fail "Linux artifact is empty: $linuxExe" }

function Format-Bytes {
    param([long]$Bytes)
    "{0:N2} MB" -f ($Bytes / 1MB)
}

Write-Host ""
Write-Host "Windows x64 artifact:" -ForegroundColor Green
Write-Host "  Path:  $winExe"
Write-Host "  Size:  $(Format-Bytes $winSize)"
Write-Host "  Files in $($winOutDir):  $($winFiles.Count)"
$winFiles | ForEach-Object { Write-Host "    - $($_.Name) ($(Format-Bytes $_.Length))" }

Write-Host ""
Write-Host "Linux x64 artifact:" -ForegroundColor Green
Write-Host "  Path:  $linuxExe"
Write-Host "  Size:  $(Format-Bytes $linuxSize)"
Write-Host "  Files in $($linuxOutDir):  $($linuxFiles.Count)"
$linuxFiles | ForEach-Object { Write-Host "    - $($_.Name) ($(Format-Bytes $_.Length))" }

# ---------------------------------------------------------------------------
# 7. Checksums
# ---------------------------------------------------------------------------
Write-Step "Generating SHA-256 checksums"

$checksumFile = Join-Path $distRoot 'SHA256SUMS.txt'
$lines = @()
$lines += "$((Get-FileHash -Algorithm SHA256 $winExe).Hash.ToLowerInvariant())  ServerSleuth-Windows-x64/ServerSleuth.exe"
$lines += "$((Get-FileHash -Algorithm SHA256 $linuxExe).Hash.ToLowerInvariant())  ServerSleuth-Linux-x64/ServerSleuth"
Set-Content -Path $checksumFile -Value $lines -Encoding ASCII
Write-Host "  Written: $checksumFile"

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Step "Release build complete"
Write-Host "  $winExe"
Write-Host "  $linuxExe"
Write-Host "  $checksumFile"
exit 0
