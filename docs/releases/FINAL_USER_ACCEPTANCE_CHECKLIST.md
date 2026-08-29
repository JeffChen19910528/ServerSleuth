# ServerSleuth v1.0.0 — Final User Acceptance Checklist

This checklist covers the items this environment could **not** validate itself — no
interactive Windows desktop session or UI automation is available here, so every GUI
click-through step below still needs a human to actually perform it once, on a real desktop,
before treating the GUI as fully accepted. Everything else (CLI on both platforms, packaging,
self-contained/single-file behavior, security, checksums, build/test regression) **was**
independently verified by Claude Code — most recently in Phase GUI-7C, the final GUI phase; see
`FINAL_RELEASE_SIGNOFF.md` for that evidence. Please check off each box as you personally
confirm it.

Updated in Phase GUI-7C to cover the now-complete GUI: Dashboard, Inventory, Migration, Reports,
and Settings are real pages as of GUI-7A/7B/7C — the original Phase 11C version of this checklist
predated all four and only covered Scan/Results.

## Windows GUI (`release/windows/ServerSleuth.exe`, or `ServerSleuth-v1.0.0-windows-x64.zip`)

### Launch & Dashboard
- [ ] GUI launches (double-click `ServerSleuth.exe`) and the main window appears with no
      exception dialog
- [ ] Before any scan, Dashboard shows a clean empty state ("No scan results yet") with a
      Start Scan button — no fabricated statistics
- [ ] Dashboard, Inventory, Results, Migration, and Reports are all reachable from the left
      navigation before any scan runs, and each shows its own real empty state (not a
      placeholder, not an error)

### Scan
- [ ] Scan Configuration page is reachable and shows Local/Remote target options
- [ ] A local scan can be started from Scan Configuration
- [ ] Scan Execution shows live progress/status while the scan runs
- [ ] The scan completes without an unexpected exception

### Dashboard (after a completed scan)
- [ ] Dashboard now shows real Entity/Application/Dependency counts and Risk/Migration summary
      numbers matching what the scan actually found
- [ ] Dashboard's "View Results", "Inventory", and "New Scan" buttons all navigate correctly

### Inventory
- [ ] Inventory (as its own navigation page, not only embedded in Results) shows category chips
      with real counts
- [ ] Selecting a category filters the item list
- [ ] Selecting an inventory item shows its detail panel (path/evidence/metadata where
      available, "Unassigned" for items with no owning application)

### Results
- [ ] Results Dashboard shows a Scan Summary
- [ ] The Applications list is visible and usable
- [ ] Clicking an application shows its Application Detail panel
- [ ] Risk information (findings, severities) is visible
- [ ] Dependency information is visible

### Migration
- [ ] Migration (as its own navigation page) shows Blocked/Needs Remediation/Ready With
      Conditions/Ready counts
- [ ] The application list is visible
- [ ] Selecting an application shows Issues / Actions / Verification Checks / Dependencies
- [ ] There is NO button anywhere on this page that executes a migration action, applies a fix,
      installs anything, or restarts a service — assessment only

### Reports
- [ ] Reports (as its own navigation page) shows the latest scan's available report files
- [ ] JSON report can be opened and its raw content is displayed as plain text
- [ ] HTML report can be opened and its raw content is displayed as plain text (not rendered as
      a web page)
- [ ] Export (JSON / HTML / Both) works and reports a result
- [ ] Overwrite-policy behavior works as configured (FailIfExists vs. Overwrite)

### Settings
- [ ] Settings page is reachable and shows Default Output Directory / Default Report Format /
      Default Overwrite Policy / Verbose Output / Language
- [ ] Changing a setting (e.g. Default Report Format) and then returning to Scan Configuration
      shows that new value as the default for a NEW scan
- [ ] The language toggle (here or in the header) switches all visible labels between English
      and Traditional Chinese

### Navigation & shutdown
- [ ] "New Scan" (from Scan Execution, Results, or Migration) returns to Scan Configuration
- [ ] Every navigation item (Dashboard/Scan/Inventory/Results/Migration/Reports/Settings) is
      reachable at any time, with no dead/unresponsive item
- [ ] Closing the GUI window ends the process cleanly, with no leftover/orphan process

## Windows CLI (`release/windows/serversleuth-cli.exe`)

- [x] `--help` prints usage — verified by Claude Code (Phase GUI-7C, from an isolated extracted
      package copy of the current build)
- [x] `--version` prints a version string (`1.0.0.0` — the assembly's own 4-part version; see
      `FINAL_RELEASE_SIGNOFF.md` for why this is the documented, intentional format) —
      verified
- [x] A real local scan completes (Discovery → Analysis → Risk → Migration → Reporting →
      Export) — verified: 34,860 entities, 12 scanners (4 partial), exit code 4
      (`PartialDiscovery` — correct semantics, not a defect)
- [x] `report.json` is produced and readable — verified
- [x] `report.html` is produced and readable — verified
- [x] Exit code matches the scan outcome — verified

## Linux CLI (`release/linux/serversleuth`)

- [x] Package rebuilt this phase from byte-for-byte unchanged CLI/backend source; end-to-end
      functional behavior (help/version/real scan/self-contained, no `dotnet` required)
      previously independently verified in Phase 11C against the prior release candidate — see
      that phase's evidence in `FINAL_RELEASE_SIGNOFF.md`'s history for the full record.
- [x] Package integrity and SHA256 of this phase's own freshly-built artifact — verified (see
      `FINAL_RELEASE_SIGNOFF.md`).

## Package

- [x] No .NET installation required (both executables are self-contained) — verified
- [x] No DLL deployment required (package contains only the executable + `README.txt` +
      `VERSION`) — verified
- [x] No source tree required (ran from an isolated extracted copy, not the repository) —
      verified
- [x] No development environment required (no `dotnet`, no NuGet cache, no build tools
      involved in running either package) — verified

---

Items marked `[x]` above were independently verified by Claude Code in this environment and
do not need to be re-checked, though you're welcome to. Items left as `[ ]` (the entire
Windows GUI section) require a human with access to a real interactive Windows desktop —
please check each one off as you confirm it, and only then consider the GUI itself formally
accepted.
