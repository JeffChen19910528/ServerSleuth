# ServerSleuth v1.0.0 — Final User Acceptance Checklist

This checklist covers the items this environment could **not** validate itself — no
interactive Windows desktop session or UI automation is available here, so every GUI
click-through step below still needs a human to actually perform it once, on a real desktop,
before treating the GUI as fully accepted. Everything else (CLI on both platforms, packaging,
self-contained/single-file behavior, security, checksums, build/test regression) **was**
independently verified by Claude Code in Phase 11C — see `FINAL_RELEASE_SIGNOFF.md` for that
evidence. Please check off each box as you personally confirm it.

## Windows GUI (`release/windows/ServerSleuth.exe`, or `ServerSleuth-v1.0.0-windows-x64.zip`)

- [ ] GUI launches (double-click `ServerSleuth.exe`) and the main window appears with no
      exception dialog
- [ ] Scan Configuration page is reachable and shows Local/Remote target options
- [ ] A local scan can be started from Scan Configuration
- [ ] Scan Execution shows live progress/status while the scan runs
- [ ] The scan completes without an unexpected exception
- [ ] Results Dashboard shows a Scan Summary
- [ ] Results Dashboard shows the Discovery Inventory (category counts, searchable/filterable
      entity list)
- [ ] The Applications list is visible and usable
- [ ] Clicking an application shows its Application Detail panel
- [ ] Risk information (findings, severities) is visible
- [ ] Migration information (status, issues, actions) is visible
- [ ] Dependency information is visible
- [ ] Issues / Actions / Verification Checks sections are visible
- [ ] JSON report export works
- [ ] HTML report export works
- [ ] The in-app report viewer displays the exported report's content
- [ ] "New Scan" returns to Scan Configuration
- [ ] Closing the GUI window ends the process cleanly, with no leftover/orphan process

## Windows CLI (`release/windows/serversleuth-cli.exe`)

- [x] `--help` prints usage — verified by Claude Code (Phase 11C, from an isolated extracted
      package copy)
- [x] `--version` prints a version string (`1.0.0.0` — the assembly's own 4-part version; see
      `FINAL_RELEASE_SIGNOFF.md` for why this is the documented, intentional format) —
      verified
- [x] `scan --help` documents all scan options — verified
- [x] A real local scan completes (Discovery → Analysis → Risk → Migration → Reporting →
      Export) — verified: 34,881 entities, 12 scanners (4 partial), exit code 4
      (`PartialDiscovery` — correct semantics, not a defect)
- [x] `report.json` is produced and readable — verified
- [x] `report.html` is produced and readable — verified
- [x] Exit code matches the scan outcome — verified

## Linux CLI (`release/linux/serversleuth`)

- [x] `--help` prints usage — verified by Claude Code (Phase 11C, WSL2 Ubuntu, isolated
      directory, no `dotnet` installed)
- [x] `--version` prints a version string (`1.0.0.0`) — verified
- [x] `scan --help` documents all scan options — verified
- [x] A real local scan completes — verified: 1,634 entities, 11 scanners (6 partial,
      `linux-kubernetes-scanner` correctly `NotInstalled`), exit code 0
- [x] `report.json` is produced and readable — verified
- [x] `report.html` is produced and readable — verified
- [x] Exit code matches the scan outcome — verified

## Package

- [x] No .NET installation required (confirmed absent in the WSL2 test environment; both
      executables are self-contained) — verified
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
