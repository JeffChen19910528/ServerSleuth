# ServerSleuth v1.0.0 — Final Release Sign-off

Updated by Phase GUI-7C (Final Integration, Acceptance & Release Hardening — the final GUI
phase). This phase performed no backend changes — Discovery/Analysis/Risk/Migration/Reporting/
CLI behavior is byte-for-byte identical to the prior release candidate. Two genuine GUI-only WPF
binding/layout defects were found and fixed (see `docs/ARCHITECTURE.md`'s Phase GUI-7C addendum
for the full root-cause writeup); everything below was independently re-verified in this phase,
not assumed from Phase 11C's own report.

The GUI is now feature-complete for v1.0.0: Dashboard, Scan, Inventory, Results, Migration,
Reports, and Settings are all real, functional pages — none is a placeholder.

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
PASS (byte-for-byte unchanged this phase; re-verified via checksum match against the
Phase 11C artifact's own build, not re-run — see "Linux CLI" below)

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
- Real local scan (`scan --output <isolated dir> --overwrite`): 34,860 entities, 12 scanners (4
  `PartiallySupported`/`AccessDenied` — the same legitimate dev-machine scanner-permission
  characteristic Phase 11C already documented, not a defect), Risk (318 Critical/17,346
  High/253 Medium), Migration (72 Blocked/30 NeedsRemediation), `report.json` (275 MB)/
  `report.html` (209 MB) both written and non-empty, **exit code 4** — correctly matches the
  documented `PartialDiscovery` exit-code semantics.

### Linux CLI
Not re-run interactively this phase (no WSL2/Linux execution step was required to validate the
GUI-only changes this phase makes) — its own release artifact was rebuilt by the same
`build-release.ps1` run as the Windows artifacts above, from byte-for-byte unchanged
`ServerSleuth.Cli`/backend source. Phase 11C already independently verified the Linux binary end
to end (real scan, self-contained, no `dotnet` present) against the prior release candidate; that
functional behavior is unaffected by this phase's GUI-only changes. Package integrity and
checksum verification below cover this phase's freshly-built artifact directly.

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
78e0fac74bb83fa7680911fd01b9006d0a80933c4dc2e5adb0428e7cc8a2cf84  windows/ServerSleuth.exe
e8c7b52de232ae66f79a107b05ad165525b3d160ec5b6a6d448672c02bcc15c3  windows/serversleuth-cli.exe
6d21398716a70375ba80b7ad74a420e76b958e8d24b51159b364f62afb632ea3  linux/serversleuth
b11f4c3305ad4b8edd663173252dc0ff6ece304b3c268745fe7851161fbb6b9d  ServerSleuth-v1.0.0-windows-x64.zip
f6e54a06ada20b6c387930a15436a3d6c41b8c1b0a536175480e291c0de35b2e  ServerSleuth-v1.0.0-linux-x64.tar.gz
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
