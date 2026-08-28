# Security Policy

## Security philosophy

ServerSleuth is a server discovery and migration-assessment tool, and its security model
follows directly from that purpose: it is a **read-only inspector**, never an actor on the
systems it examines.

- **Strictly read-only / inspection-only.** ServerSleuth never stops or restarts a service,
  modifies the registry, IIS, or systemd configuration, deletes a file, installs a package,
  changes a firewall rule, exports a private key, or otherwise mutates anything on a scanned
  target. Every scanner is a reader, never a writer, of the machine it inspects.
- **No execution of discovered or unknown binaries.** Binary analysis (PE/ELF metadata) is
  always static — ServerSleuth never calls `Assembly.LoadFrom`/`LoadLibrary`/`Process.Start`/
  COM activation on anything it discovers, and never runs an unknown or user-supplied
  executable.
- **No telemetry, no default network calls for core discovery, no automatic upload.**
  Scan output stays on the machine that ran the scan (or is written to whatever local output
  directory the user specifies) unless the user themselves copies it elsewhere.
- **No probing of external systems.** External dependencies (databases, HTTP/S APIs, LDAP,
  file shares, etc.) are identified from static configuration only — ServerSleuth never
  connects to them, and never uses a discovered credential to do so.

## Credential handling

- **Credentials never enter `ScanTarget`.** The type that identifies *what* to scan
  (`ScanTarget`) carries no username/password/key material at all — credentials for a remote
  SSH or WinRM scan are supplied separately, at the point of connection, and are never part of
  any object that gets serialized into a report, logged, or persisted to disk.
- **A password is always a `SecureString`, never a plain `string`**, from the point it is
  entered (CLI prompt / GUI credential dialog) through to the point it is handed to the
  underlying SSH/WinRM client library — mechanically enforced by dedicated architecture tests
  (e.g. `ScanCredentialInput_PasswordProperty_IsSecureString_NeverAPlainString`).
- **SSH private keys are referenced by file path, and passphrases by an environment-variable
  *name*** — ServerSleuth never reads raw key bytes or a raw passphrase value into a property
  that could be logged, displayed, or serialized; it only ever holds a path/variable-name
  string the user themselves already controls.
- **Host-key and certificate validation fail closed.** A remote connection whose host key or
  certificate cannot be validated against what the user expects is refused, not silently
  accepted — ServerSleuth does not implement a "trust on first use, ignore afterwards" or
  "accept any certificate" fallback for remote transports.

## Secret redaction in output

- Every scanner that could encounter secret-shaped text (connection strings, configuration
  files, environment variables, COM registration arguments, container/Kubernetes manifests,
  scheduled-task arguments) passes that text through a dedicated secret redactor
  (`ISecretRedactor`) **before** it is ever attached to a discovered entity, an evidence
  record, a log line, or a report. Detected values are replaced with `[REDACTED]` /
  `SecretDetected: true` — the raw value is never written to `report.json`, `report.html`,
  console/verbose output, GUI state, or application logs.
- This redaction happens once, at the point of discovery — every downstream consumer (the
  CLI, the GUI's Discovery Inventory, JSON/HTML report rendering) reads already-redacted data
  and does not re-implement or bypass this logic.
- This is covered by dedicated tests across every layer that touches potentially secret-shaped
  text (scanner-level negative-security tests, report-renderer secret-safety tests, GUI
  architecture tests asserting no credential-shaped property exists on any bound ViewModel or
  persisted state type).

## Reporting a vulnerability

Please use the repository's private security reporting mechanism if one is configured by the
project owner (for example, a GitHub "Report a vulnerability" / Security Advisories form on
the repository, if enabled). This document intentionally does not list a specific contact
address, mailbox, or issue tracker, since no such official contact channel currently exists in
this repository for this project — please do not open a public issue containing exploit
details, and never include credentials, private keys, or production configuration in any
report you file.
