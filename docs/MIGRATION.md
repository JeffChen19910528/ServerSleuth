# Migration Guide

## What ServerSleuth is — and isn't

ServerSleuth is a **migration assessment and planning tool**. It inspects a server and
produces an evidence-backed inventory plus a migration risk assessment — it is **not** a
migration execution tool, and it never becomes one automatically.

ServerSleuth does not, and will never (without a deliberate, separately-scoped future
decision):

- Automatically execute a migration action against any server.
- Modify the target server it scanned, or any other server, in any way.
- Move, copy, or provision workloads, data, or infrastructure.
- Install, uninstall, start, or stop anything.

Everything described below is **declarative planning information** — a structured, evidence-
backed answer to "what would a human need to know and do to migrate this server," not a
script that does it for them.

## What a migration assessment produces

Running ServerSleuth's `Migration` scan profile produces:

- **`MigrationStatus`** — a per-application readiness classification (e.g. Blocked, Needs
  Remediation, Ready With Conditions, Ready), derived from the risk findings attributed to
  that application's boundary.
- **`MigrationIssue`** — a specific, evidence-backed problem that affects migration readiness
  (e.g. a missing binary, an access-denied scanner result, a certificate nearing expiry), each
  tied back to the risk finding and evidence that justified it.
- **`MigrationDependency`** — a dependency (database, external API, file share, runtime,
  certificate, shared infrastructure component, etc.) an application relies on, which whoever
  performs the actual migration will need to account for on the destination.
- **`MigrationAction`** — a recommended, declarative next step (e.g. "prepare this missing
  binary on the destination," "provision this runtime version") — a suggestion for a human to
  act on, never something ServerSleuth itself runs.
- **`MigrationVerificationCheck`** — a pre-migration or post-migration check a human should
  perform to confirm readiness or success (e.g. "confirm this external dependency is reachable
  from the new environment") — again, something for a person (or a separate, human-operated
  process) to carry out, not something ServerSleuth executes.

All of the above is presented in `report.json` (machine-readable) and `report.html`
(human-readable), and — for a completed scan viewed in the desktop GUI — in the Results
Dashboard's Migration Summary, Risk Findings, Migration Issues, and Migration Actions
sections, plus the Discovery Inventory's raw entity/evidence view.

## How to use this output

1. Run a `Migration`-profile scan against the server you're assessing.
2. Review `report.html` (or the GUI's Results Dashboard) for the overall migration status per
   application, and read each `MigrationIssue`'s evidence and recommendation.
3. Use the listed `MigrationDependency` entries to build your own migration plan for what needs
   to exist on the destination environment before cutover.
4. Treat `MigrationAction`/`MigrationVerificationCheck` entries as a checklist for your own
   (human-driven) migration process — perform them yourself, using whatever tooling is
   appropriate for your environment (your own infrastructure-as-code, deployment pipeline,
   manual runbook, etc.). ServerSleuth does not provide, and is not intended to provide, that
   execution tooling.
5. Re-run ServerSleuth after remediation to confirm previously-flagged issues are resolved,
   the same way you ran it the first time — it never remembers or diffs against a prior run
   automatically; each scan is an independent, fresh assessment.

## Scope boundary

If a future need arises for ServerSleuth to *execute* part of a migration (rather than assess
and plan one), that would be a deliberate, separately-scoped, explicitly-approved capability —
it is not something this tool does today, and nothing in this document should be read as
implying otherwise.
