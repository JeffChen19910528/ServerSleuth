#!/usr/bin/env bash
#
# Phase 11A release packaging (Linux/macOS host): publishes the self-contained,
# single-file Linux x64 CLI artifact. This is the POSIX counterpart to
# build-release.ps1, which can only run on Windows (PowerShell 5.1) and also
# builds the Windows x64 WPF GUI.
#
# The WPF GUI (src/ServerSleuth.Gui, net8.0-windows) cannot be built from this
# script or on this host at all — WPF's XAML/BAML build tooling requires
# Windows regardless of which OS `dotnet` runs on. Build the Windows GUI
# artifact with build-release.ps1 on Windows; this script only produces:
#
#   Linux x64: src/ServerSleuth.Cli (net8.0 TFM) -> ServerSleuth
#
# Output goes to dist/ServerSleuth-Linux-x64/. The SHA-256 checksum line for
# this artifact is written/updated in dist/SHA256SUMS.txt without touching any
# other lines already in that file (e.g. one for the Windows artifact copied
# over from a Windows build).
#
# Usage:
#   ./build-release.sh [Configuration]   # Configuration defaults to Release

set -euo pipefail

CONFIGURATION="${1:-Release}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIST_ROOT="$REPO_ROOT/dist"
CLI_PROJECT="$REPO_ROOT/src/ServerSleuth.Cli/ServerSleuth.Cli.csproj"

step() { printf '\n==> %s\n' "$1"; }
fail() { printf 'ERROR: %s\n' "$1" >&2; exit 1; }

step "Validating prerequisites"

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK ('dotnet') was not found on PATH."
SDK_VERSION="$(dotnet --version)"
[ -n "$SDK_VERSION" ] || fail "Could not determine the installed .NET SDK version."
echo "  dotnet SDK: $SDK_VERSION"

[ -f "$CLI_PROJECT" ] || fail "CLI project not found at $CLI_PROJECT"
echo "  CLI project: $CLI_PROJECT"

LINUX_OUT_DIR="$DIST_ROOT/ServerSleuth-Linux-x64"
PUBLISH_STAGING_ROOT="$REPO_ROOT/obj/release-publish-staging"
LINUX_STAGING_DIR="$PUBLISH_STAGING_ROOT/linux-x64"

step "Cleaning Linux staging/output directories"
rm -rf "$LINUX_STAGING_DIR" "$LINUX_OUT_DIR"
mkdir -p "$DIST_ROOT" "$LINUX_OUT_DIR"
echo "  Cleaned: $LINUX_OUT_DIR"

step "Publishing Linux x64 (ServerSleuth.Cli)"

dotnet publish "$CLI_PROJECT" \
    -c "$CONFIGURATION" \
    -f net8.0 \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false \
    -o "$LINUX_STAGING_DIR"

LINUX_PUBLISHED_EXE="$LINUX_STAGING_DIR/serversleuth"
[ -f "$LINUX_PUBLISHED_EXE" ] || fail "Expected published Linux binary not found: $LINUX_PUBLISHED_EXE"

step "Assembling release artifact"

# Renaming to the user-facing "ServerSleuth" here is a publish-output rename
# only — matches build-release.ps1's handling of the same AssemblyName quirk.
cp "$LINUX_PUBLISHED_EXE" "$LINUX_OUT_DIR/ServerSleuth"
chmod +x "$LINUX_OUT_DIR/ServerSleuth"
LINUX_EXE="$LINUX_OUT_DIR/ServerSleuth"

[ -s "$LINUX_EXE" ] || fail "Linux artifact is empty: $LINUX_EXE"

step "Verifying artifact"

LINUX_SIZE=$(stat -c%s "$LINUX_EXE" 2>/dev/null || stat -f%z "$LINUX_EXE")
LINUX_SIZE_MB=$(awk -v b="$LINUX_SIZE" 'BEGIN { printf "%.2f", b / 1048576 }')
echo "  Path: $LINUX_EXE"
echo "  Size: ${LINUX_SIZE_MB} MB"

step "Updating SHA-256 checksum"

CHECKSUM_FILE="$DIST_ROOT/SHA256SUMS.txt"
LINUX_HASH=$(sha256sum "$LINUX_EXE" | awk '{print $1}')
LINUX_LINE="$LINUX_HASH  ServerSleuth-Linux-x64/ServerSleuth"

if [ -f "$CHECKSUM_FILE" ]; then
    grep -v 'ServerSleuth-Linux-x64/ServerSleuth$' "$CHECKSUM_FILE" > "$CHECKSUM_FILE.tmp" || true
    mv "$CHECKSUM_FILE.tmp" "$CHECKSUM_FILE"
fi
echo "$LINUX_LINE" >> "$CHECKSUM_FILE"
echo "  Written: $CHECKSUM_FILE"

step "Release build complete"
echo "  $LINUX_EXE"
echo "  $CHECKSUM_FILE"
echo ""
echo "Note: the Windows x64 GUI artifact must be built separately on Windows"
echo "with build-release.ps1 (WPF cannot be compiled on this platform)."
