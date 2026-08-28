#!/usr/bin/env bash
#
# Phase 11B v1.0.0 release packaging (Linux/macOS host): publishes the self-contained,
# single-file Linux x64 CLI artifact and assembles its distribution package. This is the
# POSIX counterpart to build-release.ps1, which can only run on Windows (PowerShell 5.1)
# and also builds the Windows x64 GUI and CLI.
#
# The Windows GUI (src/ServerSleuth.Gui, net8.0-windows) cannot be built from this script
# or on this host at all — WPF's XAML/BAML build tooling requires Windows regardless of
# which OS `dotnet` runs on. The Windows CLI, while technically a plain console app, also
# targets the net8.0-windows TFM (so it can compile ServerSleuth.Windows in) and so is
# likewise Windows-build-only. Build both Windows artifacts with build-release.ps1 on
# Windows; this script only produces:
#
#   Linux x64: src/ServerSleuth.Cli (net8.0 TFM) -> serversleuth
#
# Output goes to release/linux/ plus release/ServerSleuth-v<version>-linux-x64.tar.gz. The
# SHA-256 checksum lines for the Linux artifacts are written/updated in
# release/SHA256SUMS.txt without touching any other lines already in that file (e.g. the
# Windows lines copied over from a Windows build's own release/ output).
#
# Usage:
#   ./build-release.sh [Configuration]   # Configuration defaults to Release

set -euo pipefail

CONFIGURATION="${1:-Release}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_ROOT="$REPO_ROOT/release"
CLI_PROJECT="$REPO_ROOT/src/ServerSleuth.Cli/ServerSleuth.Cli.csproj"
BUILD_PROPS="$REPO_ROOT/Directory.Build.props"

step() { printf '\n==> %s\n' "$1"; }
fail() { printf 'ERROR: %s\n' "$1" >&2; exit 1; }

step "Validating prerequisites"

command -v dotnet >/dev/null 2>&1 || fail ".NET SDK ('dotnet') was not found on PATH."
SDK_VERSION="$(dotnet --version)"
[ -n "$SDK_VERSION" ] || fail "Could not determine the installed .NET SDK version."
echo "  dotnet SDK: $SDK_VERSION"

[ -f "$CLI_PROJECT" ] || fail "CLI project not found at $CLI_PROJECT"
[ -f "$BUILD_PROPS" ] || fail "Directory.Build.props not found at $BUILD_PROPS"
echo "  CLI project: $CLI_PROJECT"

# Read the version straight out of Directory.Build.props — the same single source of
# truth build-release.ps1 reads, never a second hard-coded copy.
VERSION="$(grep -o '<Version>[^<]*</Version>' "$BUILD_PROPS" | head -n1 | sed -e 's/<Version>//' -e 's/<\/Version>//')"
[ -n "$VERSION" ] || fail "Could not read <Version> from Directory.Build.props."
echo "  Release version: $VERSION"

command -v tar >/dev/null 2>&1 || fail "'tar' was not found on PATH."

LINUX_OUT_DIR="$RELEASE_ROOT/linux"
PUBLISH_STAGING_ROOT="$REPO_ROOT/obj/release-publish-staging"
LINUX_STAGING_DIR="$PUBLISH_STAGING_ROOT/linux-x64"

step "Cleaning Linux staging/output directories"
rm -rf "$LINUX_STAGING_DIR" "$LINUX_OUT_DIR"
mkdir -p "$RELEASE_ROOT" "$LINUX_OUT_DIR"
echo "  Cleaned: $LINUX_OUT_DIR"

step "Publishing Linux x64 CLI (ServerSleuth.Cli)"

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

# Only the real executable is copied out of the staging directory — never the whole
# publish output (.pdb/.deps.json/.runtimeconfig.json are harmless leftovers the
# executable itself does not need, and must never reach the shipped package).
cp "$LINUX_PUBLISHED_EXE" "$LINUX_OUT_DIR/serversleuth"
chmod +x "$LINUX_OUT_DIR/serversleuth"
LINUX_EXE="$LINUX_OUT_DIR/serversleuth"

[ -s "$LINUX_EXE" ] || fail "Linux artifact is empty: $LINUX_EXE"

step "Writing VERSION and README.txt"

echo -n "$VERSION" > "$RELEASE_ROOT/VERSION"
cp "$RELEASE_ROOT/VERSION" "$LINUX_OUT_DIR/VERSION"

cat > "$LINUX_OUT_DIR/README.txt" <<EOF
ServerSleuth v$VERSION - Linux x64
====================================

This is a self-contained, single-file distribution. No separate .NET
installation is required.

    chmod +x serversleuth
    ./serversleuth --help
    ./serversleuth --version
    ./serversleuth scan --output <output-directory>

ServerSleuth is a strictly READ-ONLY server discovery and migration assessment
tool. It never stops/restarts services, modifies the registry/IIS/systemd,
deletes files, installs packages, or changes firewall rules on the target it
scans. Secret-shaped values (passwords, connection strings, API keys, tokens,
private keys) are always redacted from every report.

Some scanners may show as PartiallySupported / NotInstalled / AccessDenied on
a given target machine — this reflects that machine's own configuration and
the permissions the scan was run with, not a defect in ServerSleuth itself.
EOF

step "Building compressed distribution package"

LINUX_TAR_NAME="ServerSleuth-v$VERSION-linux-x64.tar.gz"
LINUX_TAR_PATH="$RELEASE_ROOT/$LINUX_TAR_NAME"
rm -f "$LINUX_TAR_PATH"
tar -czf "$LINUX_TAR_PATH" -C "$LINUX_OUT_DIR" .

step "Auditing package contents"

TAR_ENTRIES="$(tar -tzf "$LINUX_TAR_PATH")"
if echo "$TAR_ENTRIES" | grep -Ei '\.(pdb|xml|deps\.json|runtimeconfig\.json|csproj|cs)$|(^|/)(bin|obj)(/|$)' >/dev/null; then
    echo "$TAR_ENTRIES"
    fail "Package content audit found a forbidden development artifact in $LINUX_TAR_NAME."
fi
echo "  Contents: $(echo "$TAR_ENTRIES" | tr '\n' ' ')"
echo "  No forbidden development artifacts found."

step "Verifying artifact"

LINUX_SIZE=$(stat -c%s "$LINUX_EXE" 2>/dev/null || stat -f%z "$LINUX_EXE")
LINUX_SIZE_MB=$(awk -v b="$LINUX_SIZE" 'BEGIN { printf "%.2f", b / 1048576 }')
echo "  Path: $LINUX_EXE"
echo "  Size: ${LINUX_SIZE_MB} MB"

step "Updating SHA-256 checksums"

CHECKSUM_FILE="$RELEASE_ROOT/SHA256SUMS.txt"
LINUX_EXE_HASH=$(sha256sum "$LINUX_EXE" | awk '{print $1}')
LINUX_TAR_HASH=$(sha256sum "$LINUX_TAR_PATH" | awk '{print $1}')

if [ -f "$CHECKSUM_FILE" ]; then
    grep -vE '^[0-9a-f]+  (linux/serversleuth|ServerSleuth-v[^ ]*-linux-x64\.tar\.gz)$' "$CHECKSUM_FILE" > "$CHECKSUM_FILE.tmp" || true
    mv "$CHECKSUM_FILE.tmp" "$CHECKSUM_FILE"
fi
{
    echo "$LINUX_EXE_HASH  linux/serversleuth"
    echo "$LINUX_TAR_HASH  $LINUX_TAR_NAME"
} >> "$CHECKSUM_FILE"
echo "  Written: $CHECKSUM_FILE"

step "Release build complete — v$VERSION"
echo "  $LINUX_EXE"
echo "  $LINUX_TAR_PATH"
echo "  $RELEASE_ROOT/VERSION"
echo "  $CHECKSUM_FILE"
echo ""
echo "Note: the Windows x64 GUI and CLI artifacts must be built separately on Windows"
echo "with build-release.ps1 (WPF, and the CLI's net8.0-windows TFM, cannot be built"
echo "on this platform)."
