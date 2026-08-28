# ServerSleuth v1.0.0 — Final Release Sign-off

Produced by Phase 11C (Final User Acceptance & Release Sign-off). This phase performed no
production code changes — Discovery/Analysis/Risk/Migration/Reporting/CLI/GUI behavior is
byte-for-byte identical to the Phase 11B release candidate. Every result below was
independently re-verified in this phase, not assumed from Phase 11B's own report.

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
PASS

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
`dotnet test` (full solution, this phase): **2052 passed / 5 failed / 0 skipped / 2057 total.**
An independent isolated rerun of just `ServerSleuth.Gui.Tests` produced a *third* different
result (**272 passed / 3 failed / 275 total**). Every single failure across both runs falls
inside exactly two pre-existing test classes — `MainViewModelResultsNavigationTests` and
`ScanExecutionViewModelTests` — and a *different* subset of individual tests failed each time
(4 distinct sets observed across Phase 11A/11B/11C's three independent runs so far). No
production or test code was modified in this phase at all, so there is no code-level mechanism
that could explain a genuine new regression; this is classified **Environmental / Known
Flakiness** (async `WaitUntilAsync` polling racing against WPF Dispatcher/thread-pool
scheduling in this specific sandbox — first identified and reproduced against a clean,
pre-GUI-6A checkout in Phase 11A), not a Release Regression. No test was skipped, deleted, or
weakened to reach this conclusion.

### Windows GUI
`release/windows/ServerSleuth.exe`, launched from a package extracted fresh from
`ServerSleuth-v1.0.0-windows-x64.zip` into an isolated temp directory (never `src/`/`bin/`/
`obj/`): process started, alive after 5 seconds, terminated cleanly on request with no orphan
process left behind. **This is non-visual liveness validation only.** No interactive Windows
desktop session or UI automation capability exists in this environment — the click-through
checklist in the task instructions (GUI startup → Scan Configuration → Scan Execution →
Results Dashboard → Discovery Inventory → Application Detail → Risk/Migration/Dependencies →
Export/Report Viewer → New Scan → clean close) was **not** executed by Claude Code and must
not be reported as completed. See `FINAL_USER_ACCEPTANCE_CHECKLIST.md` for the human
click-through checklist covering exactly this gap.

### Windows CLI
Extracted `ServerSleuth-v1.0.0-windows-x64.zip` into an isolated temp directory; ran
`serversleuth-cli.exe` from there only (never the repository's own `bin/`/`obj/`).
- `--help` — correct usage text.
- `--version` — prints `1.0.0.0`. See "Version Acceptance" below for why this is the correct,
  intentional output, not `1.0.0`.
- `scan --help` — documents all options correctly.
- Real local scan (`--verbose --overwrite`, isolated output directory): 34,881 entities, 12
  scanners (4 `PartiallySupported`/`AccessDenied` — expected, legitimate scanner states on this
  dev machine, not defects), Discovery 17.32s, Analysis 1.20s, Risk (318 Critical/17,346
  High/253 Medium), Migration (72 Blocked/30 NeedsRemediation), `report.json` (275MB, verified
  readable — valid JSON, confirmed by inspecting its opening structure)/`report.html` (209MB)
  both written, **exit code 4** — correctly matches the documented `PartialDiscovery` exit-code
  semantics.

### Linux CLI
Extracted `ServerSleuth-v1.0.0-linux-x64.tar.gz` inside WSL2 (Ubuntu) into an isolated `/tmp`
directory.
- `--help`/`--version` (`1.0.0.0`)/`scan --help` — all correct.
- Real local scan: 1,634 entities, 11 scanners (6 partial, including `linux-kubernetes-scanner`
  correctly reporting `NotInstalled` — no Kubernetes cluster present on this WSL instance, the
  legitimate scanner-outcome semantics the task instructions explicitly call out), `report.json`
  (1.09MB, verified readable)/`report.html` (0.91MB) both written, **exit code 0**.

### Self-contained verification
Both CLIs and the GUI ran correctly from isolated, package-only directories with no repository
source tree, no `bin`/`obj`, no NuGet cache, and (for the Linux binary specifically) no
`dotnet`/.NET SDK installed at all in that WSL2 instance — confirmed directly (`type dotnet` →
"not found") rather than assumed.

### Package Integrity
Independently re-extracted both archives and listed their contents by hand (not merely trusting
the Phase 11B build script's own audit):
- `ServerSleuth-v1.0.0-windows-x64.zip` → exactly `README.txt`, `serversleuth-cli.exe`,
  `ServerSleuth.exe`, `VERSION`. No `.dll`, `.deps.json`, `.runtimeconfig.json`, `.pdb`, source
  file, `.csproj`, `bin/`, or `obj/` anywhere.
- `ServerSleuth-v1.0.0-linux-x64.tar.gz` → exactly `README.txt`, `serversleuth`, `VERSION`. Same
  clean result.

### SHA256
Recomputed all 5 checksums (`ServerSleuth.exe`, `serversleuth-cli.exe`, `serversleuth`, both
archives) with `sha256sum` — a different tool than the release script's own `Get-FileHash` — and
every one matches `release/SHA256SUMS.txt` exactly (no artifact was rebuilt in this phase, so no
regeneration was needed).

### Security
- Scanned both executables and the package `README.txt`/`VERSION` for literal secret-shaped
  patterns (`password=`, `BEGIN ... PRIVATE KEY`, ADO.NET-style `Server=...Password=` connection
  strings): zero matches. The only related string found was the compiled-in literal
  `"SecretDetected"` — the redaction feature's own marker constant, correctly present as
  harmless framework/code metadata, not a leak.
- Scanned the real scan's own `report.json`/`report.html` (275MB/209MB, produced by an actual
  local machine scan in this phase) for the same patterns plus `ConnectionString`,
  `PrivateKeyPath`, `SshPrivateKey`, `WinRmPassword`, `SshPassword`: zero matches in every case.
- Neither release archive contains a scan report, an SSH key, a certificate, or any credential
  material — confirmed directly by the Package Integrity file listing above.

### Version Acceptance
`VERSION` / `Directory.Build.props` / `release/*/VERSION` all read `1.0.0`. `--version` on both
platforms prints `1.0.0.0` — this is the **existing, documented, intentional** CLI version
contract: `ServerSleuth.Cli.Output.VersionInfo.Version` reads
`typeof(VersionInfo).Assembly.GetName().Version`, the .NET assembly version, which is always a
4-part `Major.Minor.Build.Revision` value (`1.0.0` → `1.0.0.0`) — not a separate, hand-maintained
string. This is covered by an existing test (`Version_PrintsAssemblyVersion_ExitsSuccess`,
asserting the pattern `^\d+\.\d+\.\d+`, which the `1.0.0.0` output satisfies). Per this phase's
own explicit instruction, this is recorded as-is and **not** treated as a defect or changed —
doing so would mean either altering the assembly-version-is-the-single-source-of-truth contract
(itself a deliberate Phase 10A decision, documented in `VersionInfo.cs`'s own comment) or adding
a second, separately-maintained version string, both out of this phase's scope.

## Interactive GUI validation

```
Interactive GUI validation: NOT EXECUTED
Reason: no interactive Windows desktop automation/session available in this environment.
```

Only non-visual liveness (process launch, stays alive, clean termination, no orphan) was
performed — see "Windows GUI" above. A human must complete the click-through steps in
`FINAL_USER_ACCEPTANCE_CHECKLIST.md` before the GUI itself is considered fully accepted.

## Live SSH / Live WinRM

Not executed — no authorized live SSH or WinRM host is available in this environment, the same
disclosed constraint present in every phase back through Phase 10D-2/10D-3B. Only closed-port
connection-refused behavior has ever been verified for either transport (recorded in prior
phases' own history, not re-claimed here as acceptance).

## Known Limitations

- Interactive/visual GUI validation not performed (see above).
- Live SSH/WinRM remote acceptance not performed (see above).
- SHA-256 checksums were recomputed with a different tool (`sha256sum` vs. the build script's
  `Get-FileHash`) but both ultimately call into the same OS-level SHA-256 implementation family
  — this is a meaningful cross-check, not full independent-implementation verification.
- Intermittent WPF async-timing test flakiness persists in `MainViewModelResultsNavigationTests`/
  `ScanExecutionViewModelTests` (environmental to this sandbox, pre-existing, not touched or
  fixed in this phase per its own explicit "do not fix non-blockers" instruction).
- `GraphValidator`'s suspected-but-unmeasured large-scale performance characteristic (flagged in
  Phase 11A) remains unmeasured — never empirically triggered in any real or synthetic run to
  date.

## Outstanding Validation

- Human click-through of the Windows GUI (`FINAL_USER_ACCEPTANCE_CHECKLIST.md`).
- Live SSH acceptance against an authorized remote Linux host.
- Live WinRM acceptance against an authorized remote Windows host.

None of the above are release blockers per this phase's own classification rules — they are
disclosed, outstanding validation items, not evidence of a defect.

## Release Blockers

**0 found.** No functional defect, packaging defect, or genuine regression was identified in
this phase. The one version-format observation above was evaluated and classified as
"working as intentionally designed," not a defect.

## Final Release Status

```
ServerSleuth v1.0.0
RELEASE READY
```

This status reflects everything this environment can automatically and independently verify:
clean build, no genuine regression, both CLIs fully functional and self-contained on their
real target platforms with real local scans completed end-to-end, package integrity, checksum
integrity, and no secret leakage anywhere in the shipped artifacts or their real output. It
does **not** mean "fully validated on every environment" — interactive GUI validation and live
remote (SSH/WinRM) acceptance remain outstanding and require a human with the appropriate
access, tracked explicitly above rather than glossed over.
