# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository State

This is no longer a greenfield project. Phases 1 through 7B of `skill.md`'s Development Process (§47) are implemented and tested — Core domain/Evidence/scanner-interface models, common Infrastructure, Windows discovery (IIS/COM/Services/Software/ScheduledTasks/Certificates/Runtimes/Registry), Linux discovery (systemd/packages/cron/Docker/Podman/Kubernetes/configuration/native ELF dependencies), cross-platform orchestration (`DiscoveryScannerRegistry`/`DiscoveryEngine`), the Correlation/Boundary/Expansion/Validation Analysis pipeline, the Evidence-Based Risk Engine (`ServerSleuth.Analysis.Risk`), and Risk Scoring & Aggregation (`ServerSleuth.Analysis.Risk.Aggregation`). See `PROGRESS.md` for the full per-phase build/test history and `ARCHITECTURE.md` for the as-built architecture (including per-phase Addendum sections) — both are authoritative over this file for what currently exists. As of the end of Phase 7B, the full solution builds with 0 warnings/errors and 1031/1031 tests pass across `ServerSleuth.Core.Tests`, `ServerSleuth.Analysis.Tests`, `ServerSleuth.Infrastructure.Tests`, `ServerSleuth.Integration.Tests`, `ServerSleuth.Linux.Tests`, and `ServerSleuth.Windows.Tests`.

Before starting new work, check `PROGRESS.md`'s last entry and `IMPLEMENTATION_PLAN.md`'s per-phase `**Status**` lines to see what's already done — do not re-implement or restart a phase that's already complete. Migration Assessment, Reporting, GUI, and CLI production hardening remain explicitly out of scope until further confirmation, per each completed phase's own strict-stop condition.

## What This Project Is

A cross-platform (Windows Server + Linux Server) **Server Discovery and Migration Assessment Tool**, targeting .NET 8+ LTS. It inspects a server and produces an evidence-backed inventory of services, IIS sites, COM components, installed software, runtimes/SDKs, processes, ports, scheduled tasks, containers, certificates, databases, and their dependencies — culminating in a migration risk assessment, dependency graph, and migration checklist. It is strictly read-only/inspection-only (see "Non-negotiable constraints" below).

Read `skill.md` in full before implementing anything — it is long (52 sections) and dense with specific requirements that are easy to violate by assumption (e.g., exact registry paths, confidence bands, risk categories, redaction rules).

## Intended Architecture (from skill.md §4)

Once implementation begins, this is the target project layout — platform-specific code must never leak into `Core`:

```
src/
├── ServerDiscovery.Core/            # Models, Interfaces, Enums, Evidence, Graph, Services (platform-agnostic)
├── ServerDiscovery.Infrastructure/   # Common/FileSystem/Networking/Process abstractions
├── ServerDiscovery.Windows/          # Services, IIS, Registry, COM, Software, ScheduledTasks, Certificates, Runtimes
├── ServerDiscovery.Linux/            # Systemd, Packages, Cron, Docker, Runtimes, Services
├── ServerDiscovery.Analysis/         # Dependency, Risk, Migration, Correlation — kept separate from discovery
├── ServerDiscovery.Reporting/        # Json, Csv, Html
└── ServerDiscovery.Cli/
```

Key architectural rules baked into the spec (violating these is a design bug, not a style nit):

- **Discovery, Correlation, Analysis, and Reporting are separate layers.** A scanner never does correlation or risk scoring itself; correlation rules must be explicit/testable, not hidden heuristics inside scanners.
- **Every scanner implements `IDiscoveryScanner`** (Id, PlatformSupport, `ScanAsync(DiscoveryContext, CancellationToken)`) and is registered in a scanner registry (`list-scanners` CLI command surfaces it).
- **Evidence is first-class.** Every discovered entity must carry `Evidence[]` records (Registry/FileSystem/Process/Command/IISConfiguration/SystemdConfiguration/PackageManager/NetworkSocket/EnvironmentVariable/ConfigurationFile/DockerInspect/ScheduledTask/CertificateStore/PE/ELF metadata) so a report can answer "why did the tool conclude this exists?"
- **Confidence scoring** uses fixed bands (0.90–1.00 Very High ... 0.00–0.24 Very Low) — never present inference as fact.
- **Status vocabulary is distinct and must not be collapsed**: Installed / Configured / Running / Listening / Referenced / Used / Unknown. "Installed" never implies "used."
- **Scanner outcome vocabulary**: Supported / PartiallySupported / AccessDenied / NotApplicable / NotInstalled / Failed — never silently skip a permission failure.
- **Fault isolation**: one scanner failing must not abort the scan; failures are recorded and the run continues, ending in a Scan Summary (Successful/Partial/Failed/Skipped counts).
- **Deduplication**: the same logical component found via multiple scanners (e.g., Oracle Client via registry + filesystem + process) must merge into one entity with multiple evidence records, not duplicate entries.

## Non-negotiable constraints (skill.md §34–36, §50)

- **Strictly read-only.** Never stop/restart services, modify registry/IIS/systemd, delete files, install packages, change firewall rules, or export private keys.
- **Never expose secrets.** Detect patterns like `Password=`, `ConnectionString=`, `API_KEY=`, `TOKEN=`, `SECRET=`, `PRIVATE_KEY=` and emit `[REDACTED]` / `SecretDetected: true` instead of the value — this applies to reports, logs, and Docker/env var discovery alike.
- **Never execute discovered/unknown binaries.** Static analysis (PE/ELF metadata) is preferred over execution.
- **No telemetry, no external network calls for core discovery**, no uploading scan data anywhere by default.
- **Do not probe external systems** (databases, APIs) — static/configuration-based detection only, and never connect using discovered credentials.
- **Commands may be unavailable** (e.g. `dotnet`, `java`, `node`) — a failed command must degrade to `NotDetected`, never an exception that kills the scan.

## Scan Profiles (skill.md §27)

Profiles are additive: `Quick` ⊂ `Standard` ⊂ `Deep` ⊂ `Migration`. When adding a new scanner, decide which profile tier it belongs to per the spec's breakdown (e.g., COM/DLL/Configuration analysis is Deep-only; risk analysis/dependency graph/migration checklist are Migration-only).

## Output contract (skill.md §29–30, §46)

A migration scan produces a fixed output directory shape (`report.html`, `inventory.json`, `inventory.csv`, `dependency-graph.json`, `migration-checklist.md`, `risks.json`, `metadata.json`) and a versioned JSON schema (`schemaVersion` field) — treat schema changes as breaking-change events requiring a version bump, not silent field additions/removals.

## Documentation the spec expects to exist (skill.md §48)

As implementation proceeds, maintain: `README.md`, `ARCHITECTURE.md`, `IMPLEMENTATION_PLAN.md`, `SECURITY.md`, `SCANNERS.md`, `MIGRATION.md`, `PROGRESS.md`, `CHANGELOG.md`. Each completed scanner needs its own documentation entry (Purpose, Supported Platforms, Data Sources, Permissions, Entities Produced, Evidence Produced, Known Limitations, Security Considerations, Tests). None of these files exist yet.

## Definition of Done for a scanner (skill.md §49)

Not done until: implementation + correct interface + unit tests + error handling + permission-failure handling + evidence generation + duplicate handling + docs + JSON serialization + HTML report inclusion + correlation rules (where applicable) + green build + passing tests.
