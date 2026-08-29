# ServerSleuth v1.0.0 — Final Release Sign-off

Updated by Phase GUI-7C (Final Integration, Acceptance & Release Hardening — the final GUI
phase). This phase performed no backend changes — Discovery/Analysis/Risk/Migration/Reporting/
CLI behavior is byte-for-byte identical to the prior release candidate. Two genuine GUI-only WPF
binding/layout defects were found and fixed (see `docs/ARCHITECTURE.md`'s Phase GUI-7C addendum
for the full root-cause writeup); everything below was independently re-verified in this phase,
not assumed from Phase 11C's own report.

The GUI is now feature-complete for v1.0.0: Dashboard, Scan, Inventory, Results, Migration,
Reports, and Settings are all real, functional pages — none is a placeholder.

Further updated by the subsequent **Release Build Pipeline Audit** task — a packaging-only
verification pass (no source/GUI/CLI code changed) confirming `build-release.ps1`/
`build-release.sh` still correctly publish this exact source tree after GUI-7A/7B/7C. Zero
script defects were found; zero lines of either script were changed. The checksums/evidence
below are from that audit's own fresh, independent rebuild (superseding the GUI-7C session's
figures, which were from a build made one docs-only commit earlier — functionally identical
binaries, this is simply the freshest re-verification; single-file publishes embed a
per-build bundle identifier, so byte-identical source still produces different checksums
across separate `dotnet publish` runs — expected, not a defect).

```
Build:
PASS

Regression:
PASS (with disclosed pre-existing environmental flakiness — see below)

Windows GUI:
NOT FULLY VALIDATED (process launch/liveness confirmed; interactive visual validation
not executed — see "Interactive GUI" below)

Windows CLI:
PASS

Linux CLI:
PASS (re-run directly in an isolated WSL2 instance with dotnet confirmed absent —
see "Linux CLI" below)

Single-file:
PASS

Self-contained:
PASS

Package Integrity:
PASS

SHA256:
PASS

Security:
PASS

Interactive GUI:
NOT AVAILABLE

Live SSH:
NOT EXECUTED

Live WinRM:
NOT EXECUTED

Release Blockers:
0 / 0

Final Status:
RELEASE READY
```

## Evidence

### Build
`dotnet build` (full solution): 0 warnings, 0 errors.

### Regression
`dotnet test` (full solution, this phase, independent per-project runs):

```
ServerSleuth.Core.Tests:               61 passed, 0 failed, 0 skipped
ServerSleuth.Analysis.Tests:          445 passed, 0 failed, 0 skipped
ServerSleuth.Infrastructure.Tests:    171 passed, 0 failed, 0 skipped
ServerSleuth.Linux.Tests:             345 passed, 0 failed, 0 skipped
ServerSleuth.Windows.Tests:           376 passed, 0 failed, 0 skipped
ServerSleuth.Cli.Tests (net8.0):       98 passed, 0 failed, 0 skipped
ServerSleuth.Cli.Tests (net8.0-win):  104 passed, 0 failed, 0 skipped
ServerSleuth.Reporting.Tests:         146 passed, 0 failed, 0 skipped
ServerSleuth.Integration.Tests:        14 passed, 0 failed, 0 skipped
ServerSleuth.Gui.ExecutionHost.Tests:  22 passed, 0 failed, 0 skipped
ServerSleuth.Gui.Tests:                370 total; 369-370 passed per run
```

Every non-GUI project passes 100%, confirming zero backend regression (no backend project was
touched this phase at all). `ServerSleuth.Gui.Tests` shows 0-1 intermittent failure per run,
always inside the pre-existing `MainViewModelResultsNavigationTests`/`ScanExecutionViewModelTests`
classes (an async `WaitUntilAsync` polling race against WPF Dispatcher/thread-pool scheduling in
this specific sandbox — first identified in Phase 11A, re-confirmed independently in GUI-6A,
GUI-7A, GUI-7B, and again here by isolating the affected test and observing it pass instantly on
an immediate rerun). Classified **Environmental / Known Flakiness**, not a Release Regression. No
test was skipped, deleted, or weakened to reach this conclusion — two GUI-7A-era tests whose
premise GUI-7B intentionally invalidated (asserting Migration/Reports/Settings were still
placeholders) were updated in GUI-7B itself, not this phase, and are documented in `PROGRESS.md`'s
GUI-7B entry.

### GUI defects found and fixed this phase
Two genuine WPF binding/layout defects, found via a real inspection sweep (not assumed clean):
1. `InventoryExplorerView.xaml` had three `Run`-hosted bindings that could throw
   `InvalidOperationException` the same way a Dashboard binding already did in GUI-7A, once the
   standalone Inventory page was shown with real (non-empty) data — fixed by converting to plain
   `TextBlock.Text` bindings.
2. `InventoryExplorerView.xaml`/`SettingsView.xaml` had no `ScrollViewer` of their own (every
   other top-level page does), so a tall detail panel could be clipped and unreachable on a
   shorter/resized window — fixed by adding one to each.

Both were caught and re-verified via `ServerSleuth.Gui.RealWindowHarness`, extended this phase to
give its fixture real discovery data and select an Inventory item, a Migration application, and
open a Reports file — all seven pages, real data, a real `Application`/`MainWindow`/layout pass,
with and without a language switch. Exit code 0 after both fixes. See `docs/ARCHITECTURE.md`'s
Phase GUI-7C addendum for the full root-cause detail.

### Windows GUI
`release/windows/ServerSleuth.exe`, rebuilt fresh this phase (`build-release.ps1`, full clean
run) and copied to an isolated temp directory (never `src/`/`bin/`/`obj/`): process started,
alive after 5 seconds, terminated cleanly on request with no orphan process left behind. **This
is non-visual liveness validation only.** No interactive Windows desktop session or UI automation
capability exists in this environment — the full click-through checklist (now covering all seven
pages) was **not** executed by Claude Code and must not be reported as completed. See
`FINAL_USER_ACCEPTANCE_CHECKLIST.md` for the human click-through checklist covering exactly this
gap.

### Windows CLI
Extracted the freshly-rebuilt `serversleuth-cli.exe` into an isolated temp directory; ran it from
there only (never the repository's own `bin/`/`obj/`).
- `--version` — prints `1.0.0.0`. Same existing, documented, intentional assembly-version
  contract as every prior phase (`ServerSleuth.Cli.Output.VersionInfo.Version` reads the .NET
  assembly version directly — not a separately-maintained string); not treated as a defect.
- `--help` — correct usage text.
- `scan --help` — documents all options correctly.
- Real local scan (`scan --output <isolated dir> --overwrite`): 34,881 entities, 12 scanners (4
  `PartiallySupported`/`AccessDenied` — the same legitimate dev-machine scanner-permission
  characteristic Phase 11C already documented, not a defect), `report.json` (275 MB)/
  `report.html` (209 MB) both written and non-empty, **exit code 4** — correctly matches the
  documented `PartialDiscovery` exit-code semantics.

### Linux CLI
Re-run directly this time (Release Build Pipeline Audit task): the freshly-built
`release/linux/serversleuth` was copied into an isolated `/tmp` directory inside a real WSL2
Ubuntu instance, where `dotnet` was first confirmed genuinely absent (`type dotnet` → not
found). From there: `--help`/`--version` (`1.0.0.0`)/`scan --help` all correct, and a real local
scan (`scan --output ./out --overwrite`) completed — 1,635 entities, 11 scanners (6 partial,
consistent with this WSL2 instance's own known characteristics, e.g. no Kubernetes cluster
present), `report.json`/`report.html` both written, **exit code 4** (`PartialDiscovery`,
correct). This directly confirms genuine self-containment, not merely that the publish command
included `--self-contained true`.

### Self-contained verification
The Windows GUI and CLI both ran correctly from an isolated, package-only directory with no
repository source tree, no `bin`/`obj`, and no NuGet cache present.

### Package Integrity
Re-extracted the freshly-built Windows archive and listed its contents by hand (not merely
trusting the build script's own audit):
- `ServerSleuth-v1.0.0-windows-x64.zip` → exactly `README.txt`, `serversleuth-cli.exe`,
  `ServerSleuth.exe`, `VERSION`. No `.dll`, `.deps.json`, `.runtimeconfig.json`, `.pdb`, source
  file, `.csproj`, `bin/`, or `obj/` anywhere.
- `ServerSleuth-v1.0.0-linux-x64.tar.gz` → exactly `README.txt`, `serversleuth`, `VERSION`. Same
  clean result.

### SHA256
Recomputed all 5 checksums (`ServerSleuth.exe`, `serversleuth-cli.exe`, `serversleuth`, both
archives) with `sha256sum` — a different tool than the release script's own `Get-FileHash` — and
every one matches `release/SHA256SUMS.txt` exactly.

```
b62c1811f7918b07ffa02811342f64477a515a6ea760113f4b4e1951f5b7f835  windows/ServerSleuth.exe
32436017a7dc79b96d5e897b0cf8de4e2127254540c6aa612244fc7b995ba2cd  windows/serversleuth-cli.exe
f1e53c3561f7fd8016c87c3709ecd69bb21e1bebcdf884c03a36bdaa99561635  linux/serversleuth
4a9fc33c7319615ef64ef1071d76db54fd20106b387c63ef7877161f2d5cfc0e  ServerSleuth-v1.0.0-windows-x64.zip
ed942dd2f9850320cf567ea8797bb1901f292f3e4e9982baf9af3e0ffa799613  ServerSleuth-v1.0.0-linux-x64.tar.gz
```

### Security
- Scanned both freshly-built executables and the package `README.txt`/`VERSION` for literal
  secret-shaped patterns (`password=`, `BEGIN ... PRIVATE KEY`, ADO.NET-style
  `Server=...Password=` connection strings): zero matches.
- Scanned this phase's own real scan output (`report.json`/`report.html`, 275 MB/209 MB) for the
  same patterns: zero matches.
- Neither release archive contains a scan report, an SSH key, a certificate, or any credential
  material — confirmed directly by the Package Integrity file listing above.
- GUI-side: `NoCredentialShapedGuiStateTests`/`ResultsDashboardSecurityBoundaryTests` re-run and
  passing across all seven pages' ViewModels — no Password/SecureString/Credential/SSH-private-
  key/token/bearer-token/secret property reachable anywhere in the GUI.

### Version Acceptance
Unchanged from Phase 11C's own finding — `--version` prints `1.0.0.0`, the intentional 4-part
.NET assembly-version format, not a defect. See that phase's original writeup (still accurate)
for the full rationale.

## Interactive GUI validation

```
Interactive GUI validation: NOT EXECUTED
Reason: no interactive Windows desktop automation/session available in this environment.
```

Only non-visual liveness (process launch, stays alive, clean termination, no orphan) was
performed — see "Windows GUI" above. A human must complete the click-through steps in
`FINAL_USER_ACCEPTANCE_CHECKLIST.md` (now covering all seven pages, updated this phase) before
the GUI itself is considered fully accepted.

## Live SSH / Live WinRM

Not executed — no authorized live SSH or WinRM host is available in this environment, the same
disclosed constraint present in every phase back through Phase 10D-2/10D-3B.

## Known Limitations

- Interactive/visual GUI validation not performed (see above).
- Live SSH/WinRM remote acceptance not performed (see above).
- Intermittent WPF async-timing test flakiness persists in `MainViewModelResultsNavigationTests`/
  `ScanExecutionViewModelTests` (environmental to this sandbox, pre-existing since Phase 11A, not
  touched or fixed in this phase per its own explicit "do not fix non-blockers" instruction).
- No Appearance/Theme setting in Settings — no existing theme-switching infrastructure to build
  on (GUI-7B decision, unchanged).
- Migration/Reports/Inventory rebuild on every navigation — no cross-navigation
  selection/filter persistence (an intentional GUI-7A/7B simplicity trade-off, not a defect).

## Outstanding Validation

- Human click-through of the Windows GUI (`FINAL_USER_ACCEPTANCE_CHECKLIST.md`), now covering
  Dashboard/Scan/Inventory/Results/Migration/Reports/Settings.
- Live SSH acceptance against an authorized remote Linux host.
- Live WinRM acceptance against an authorized remote Windows host.

None of the above are release blockers per this and every prior phase's own classification
rules — they are disclosed, outstanding validation items, not evidence of a defect.

## Release Blockers

**0 found.** No functional defect, packaging defect, or genuine regression remains unaddressed.
The two GUI binding/layout defects found this phase were fixed and re-verified before this
sign-off was written.

## Final Release Status

```
ServerSleuth v1.0.0
RELEASE READY
```

This status reflects everything this environment can automatically and independently verify:
clean build, no genuine regression, a fully feature-complete GUI (all seven pages real and
functional) with two found-and-fixed binding/layout defects, both CLIs functional and
self-contained, package integrity, checksum integrity, and no secret leakage anywhere in the
shipped artifacts or their real output. It does **not** mean "fully validated on every
environment" — interactive GUI validation and live remote (SSH/WinRM) acceptance remain
outstanding and require a human with the appropriate access, tracked explicitly above rather
than glossed over.
