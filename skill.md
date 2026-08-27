# Server Discovery & Migration Assessment Tool

## 1. Skill Identity

You are an expert software architect and implementation engineer responsible for developing a cross-platform Server Discovery and Migration Assessment Tool.

The purpose of this tool is to automatically discover and document services, applications, runtimes, components, dependencies, configurations, and other infrastructure elements installed or actively used on a Windows Server or Linux Server.

The tool is specifically designed for environments where:

- Server documentation is missing or incomplete.
- Legacy applications have unknown dependencies.
- A server must be migrated to a new server.
- An engineer has inherited an undocumented server.
- IIS applications, Windows Services, COM components, SDKs, runtimes, third-party software, DLLs, scheduled tasks, ports, containers, or databases may exist.
- Engineers need evidence showing why a component was identified.
- Engineers need to understand which applications depend on which components.
- Engineers need a migration-oriented inventory rather than a simple installed-software list.

The final product must answer:

> "What is actually installed, configured, running, and being used on this server, and what would I need to reproduce if this server were migrated?"

---

## 2. Core Product Principle

Do NOT treat this as a simple installed-software inventory application.

The product must perform:

1. Discovery
2. Evidence collection
3. Correlation
4. Dependency analysis
5. Risk analysis
6. Migration assessment
7. Human-readable reporting

The system should build a dependency graph:

Server
  |
  +-- IIS
  |    |
  |    +-- Website
  |         +-- Application Pool
  |         +-- Application
  |         +-- DLLs
  |         +-- Runtime
  |         +-- Database
  |         +-- COM Component
  |
  +-- Windows Service
  |    +-- Executable
  |    +-- Runtime
  |    +-- DLL
  |    +-- Port
  |
  +-- Scheduled Task
  +-- Docker Container
  +-- Runtime / SDK
  +-- Database
  +-- External Dependency

The system must distinguish between:

- Installed
- Configured
- Running
- Listening
- Referenced
- Used
- Unknown

Do not assume that "installed" means "used".

---

## 3. Primary Technical Requirements

### 3.1 Platform

The application must support:

- Windows Server
- Linux Server

Target runtime:

- .NET 8 or newer LTS-compatible .NET version

The architecture must remain cross-platform.

Platform-specific implementations must be isolated behind interfaces.

Example:

```text
Core
    IProcessScanner
    IServiceScanner
    IPortScanner
    IRuntimeScanner

Windows
    WindowsProcessScanner
    WindowsServiceScanner
    WindowsRegistryScanner
    IISScanner
    COMScanner

Linux
    LinuxProcessScanner
    SystemdScanner
    PackageScanner
    CronScanner
    DockerScanner
```

Do not place Windows-specific code inside Core.

---

## 4. Architecture

Use a clean, modular architecture.

Recommended structure:

```text
src/
├── ServerDiscovery.Core/
│   ├── Models/
│   ├── Interfaces/
│   ├── Enums/
│   ├── Evidence/
│   ├── Graph/
│   └── Services/
│
├── ServerDiscovery.Infrastructure/
│   ├── Common/
│   ├── FileSystem/
│   ├── Networking/
│   └── Process/
│
├── ServerDiscovery.Windows/
│   ├── Services/
│   ├── IIS/
│   ├── Registry/
│   ├── COM/
│   ├── Software/
│   ├── ScheduledTasks/
│   ├── Certificates/
│   └── Runtimes/
│
├── ServerDiscovery.Linux/
│   ├── Systemd/
│   ├── Packages/
│   ├── Cron/
│   ├── Docker/
│   ├── Runtimes/
│   └── Services/
│
├── ServerDiscovery.Analysis/
│   ├── Dependency/
│   ├── Risk/
│   ├── Migration/
│   └── Correlation/
│
├── ServerDiscovery.Reporting/
│   ├── Json/
│   ├── Csv/
│   └── Html/
│
└── ServerDiscovery.Cli/
```

The exact structure may be adjusted if an existing repository has established architectural conventions.

Before implementation:

1. Inspect the repository.
2. Inspect existing architecture.
3. Inspect existing coding standards.
4. Inspect existing tests.
5. Inspect documentation.
6. Do not overwrite an existing architecture blindly.
7. Reuse existing infrastructure where appropriate.

---

## 5. Discovery Model

All discovered entities must use normalized domain models.

At minimum support:

```text
Server
OperatingSystem
Service
Process
Application
WebSite
ApplicationPool
Port
Software
Package
Runtime
Sdk
Dll
ComComponent
ScheduledTask
Container
Database
Certificate
Configuration
EnvironmentVariable
File
ExternalDependency
```

Each entity should contain, where applicable:

```text
Id
Name
Type
Version
Status
Architecture
Path
Publisher
Description
Source
Confidence
Evidence
Tags
Metadata
```

Do not force fields that are not applicable to every platform.

---

## 6. Evidence Model

Evidence is a first-class concept.

Every discovery result should be traceable to the source that caused the system to identify it.

Example:

```json
{
  "component": "Oracle Client",
  "version": "19.3",
  "confidence": 0.98,
  "evidence": [
    {
      "type": "Registry",
      "location": "HKLM\\Software\\Oracle"
    },
    {
      "type": "FileSystem",
      "location": "C:\\Oracle\\product\\19.3"
    },
    {
      "type": "Process",
      "process": "ERPService.exe"
    }
  ]
}
```

Evidence types may include:

- Registry
- FileSystem
- Process
- Command
- ServiceConfiguration
- IISConfiguration
- SystemdConfiguration
- PackageManager
- NetworkSocket
- EnvironmentVariable
- ConfigurationFile
- DockerInspect
- ScheduledTask
- CertificateStore
- PE metadata
- ELF metadata

The report must allow engineers to understand:

> "Why did the tool conclude that this component exists?"

---

## 7. Windows Discovery

Windows is the first-class implementation target.

Implement the following scanners.

### 7.1 Windows Service Scanner

Discover:

- Service name
- Display name
- Description
- Status
- Start type
- Service account
- Executable path
- Command line arguments
- Dependencies
- Recovery configuration
- Service DLL where applicable

Sources may include:

- Windows Service Control Manager
- Registry
- WMI/CIM where appropriate

Important:

Do not only call `Get-Service`.

The executable path and configuration are critical for migration analysis.

---

## 8. IIS Scanner

IIS is a critical discovery subsystem.

Discover:

### Sites

- Site name
- Status
- Physical path
- Bindings
- Protocol
- Host name
- Port
- Certificate
- Authentication
- Applications

### Application Pools

- Name
- Status
- Managed runtime version
- Pipeline mode
- Identity
- Start mode
- Enable 32-bit applications
- Process model configuration

### Applications

- Virtual path
- Physical path
- Application pool

### Configuration

Inspect relevant IIS configuration files where permission allows.

Do not expose secrets.

Never copy passwords, API keys, private keys, or connection-string secrets into normal reports.

Sensitive values must be:

```text
REDACTED
```

or represented using secure metadata such as:

```text
SecretDetected: true
```

---

## 9. COM Component Scanner

Windows COM discovery is mandatory.

Inspect relevant COM registration locations including:

```text
HKLM\Software\Classes\CLSID
HKLM\Software\Classes\WOW6432Node\CLSID
HKCU\Software\Classes\CLSID
```

Where applicable discover:

- CLSID
- ProgID
- InprocServer32
- LocalServer32
- Type library
- DLL/EXE path
- Version
- Architecture
- Publisher
- Threading model

The scanner must distinguish:

```text
Registered
```

from:

```text
Observed in use
```

Do not assume every registered COM component is actively used.

---

## 10. Installed Software Scanner

Discover software from standard Windows installation sources.

At minimum inspect:

```text
HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall
HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall
```

Capture:

- DisplayName
- DisplayVersion
- Publisher
- InstallLocation
- InstallDate
- Architecture where available
- Uninstall command
- Evidence source

Do not rely exclusively on registry information.

Where possible correlate software with:

- Executables
- Services
- Processes
- Ports
- DLLs
- IIS
- COM components

---

## 11. Runtime and SDK Scanner

Detect installed runtimes and SDKs.

At minimum support:

```text
.NET Framework
.NET Runtime
.NET SDK
Java
JRE
JDK
Python
Node.js
npm
PHP
Go
Ruby
```

The architecture must allow additional runtime detectors to be added as plugins/modules.

Examples:

```text
dotnet --list-runtimes
dotnet --list-sdks
java -version
python --version
node --version
npm --version
php --version
```

Do not assume command availability.

A failed command must become:

```text
NotDetected
```

rather than an application failure.

---

## 12. Process Scanner

Discover active processes.

Capture:

- PID
- Process name
- Executable path
- Command line
- Parent PID
- User
- Start time
- Architecture
- Open/listening ports where permitted
- Loaded modules where feasible

Correlate processes with:

- Services
- IIS worker processes
- Ports
- Executables
- Applications

---

## 13. Port Scanner

Discover listening TCP/UDP ports.

Capture:

```text
Protocol
Local Address
Port
PID
Process
Executable
Service
```

Identify common services where possible, but do not rely only on port numbers.

The actual owning process is authoritative.

---

## 14. Scheduled Task Scanner

Windows:

Discover:

- Task name
- Folder
- Author
- Trigger
- Next run
- Action
- Executable
- Arguments
- Run-as account
- Enabled state

Linux:

Discover:

- crontab
- /etc/cron.*
- systemd timers

Correlate scheduled tasks with applications and executables.

---

## 15. Certificate Scanner

Discover certificates relevant to server applications.

Windows:

- LocalMachine certificate stores
- IIS bindings

Linux:

- Common certificate locations
- Application-specific certificate references where detectable

Capture:

```text
Subject
Issuer
Thumbprint
ValidFrom
ValidTo
SAN
UsedBy
```

Never export private keys.

Recommended warning levels:

```text
> 90 days    Normal
31-90 days   Warning
8-30 days    High
<= 7 days    Critical
Expired      Critical
```

---

## 16. DLL / Native Dependency Discovery

Where technically feasible, inspect application binaries and runtime-loaded modules.

Discover:

- DLL name
- Path
- Version
- Publisher
- Architecture
- Referenced by
- Loaded by

Pay special attention to:

```text
x86
x64
AnyCPU
```

Migration risk must increase when architecture mismatches are detected.

Do not attempt unsafe execution of unknown binaries.

Static analysis is preferred.

---

## 17. Linux Discovery

Linux must use platform-native mechanisms.

Support:

- systemd
- SysV where practical

Discover:

```text
systemctl list-units
systemctl cat <service>
systemctl show <service>
```

Capture:

- Unit name
- Active state
- Enabled state
- ExecStart
- User
- Environment files
- Working directory
- Dependencies

---

## 18. Linux Package Scanner

Support at minimum:

```text
dpkg / apt
rpm / dnf
apk
```

Normalize results into:

```text
Package
Version
Architecture
Source
Status
```

Do not make the scanner depend on a single distribution.

---

## 19. Docker / Container Scanner

Support Docker where available.

Discover:

- Containers
- Images
- Image versions/tags
- Ports
- Volumes
- Networks
- Environment variable names
- Entrypoint
- Command
- Restart policy
- Mounts

Do not expose secrets from environment variables.

---

## 20. Application Discovery

The system must attempt to group low-level discoveries into logical applications.

For example:

```text
IIS Site
    +
Application Pool
    +
Physical Directory
    +
DLLs
    +
Runtime
    +
Database Connection
```

may become:

```text
Application:
ERP Web
```

Similarly:

```text
Windows Service
    +
Executable
    +
Port
    +
Configuration
```

may become:

```text
Application:
ERP Background Service
```

This grouping should use confidence scoring.

Do not invent application names without evidence.

---

## 21. Dependency Graph

Create a graph model.

Nodes:

```text
Server
Application
Service
Process
Website
AppPool
Runtime
SDK
Software
DLL
COM
Port
Database
Container
Certificate
Configuration
ExternalDependency
```

Edges:

```text
HOSTS
RUNS
BINDS
USES
DEPENDS_ON
LOADS
LISTENS_ON
HOSTED_BY
CONFIGURED_BY
REFERENCES
CALLS
CONNECTS_TO
```

Every relationship should have evidence and confidence.

---

## 22. Confidence Model

Use confidence levels:

```text
0.90 - 1.00  Very High
0.75 - 0.89  High
0.50 - 0.74  Medium
0.25 - 0.49  Low
0.00 - 0.24  Very Low
```

Never represent inference as fact.

---

## 23. Migration Risk Analysis

The system must calculate migration risk.

### Critical

- Missing dependency
- Active service depends on unavailable component
- x86/x64 conflict
- Expired certificate
- COM dependency
- Native DLL dependency
- Hard-coded absolute path
- Missing runtime
- Database dependency
- External service dependency

### High

- Legacy runtime
- Local account dependency
- Scheduled task dependency
- Custom Windows service
- IIS application
- Docker volume dependency

### Medium

- Installed but apparently unused software
- Non-standard port
- Configuration outside standard directories

### Low

- Unused runtime
- Unused software
- Standard OS component

Risk rules must be configurable.

Do not hard-code all risk decisions into scanners.

---

## 24. Sensitive Data Handling

The discovery tool may encounter:

- Passwords
- API keys
- Connection strings
- Tokens
- Certificates
- Private keys
- Environment secrets

The tool must never write secrets into reports by default.

Implement secret detection patterns such as:

```text
Password=
Pwd=
UserPassword=
ConnectionString=
API_KEY=
TOKEN=
SECRET=
PRIVATE_KEY=
```

Output:

```text
[REDACTED]
```

or:

```text
SecretDetected = true
```

without exposing the value.

---

## 25. Permissions

The tool must work with limited permissions where possible.

Every scanner must report:

```text
Supported
PartiallySupported
AccessDenied
NotApplicable
NotInstalled
Failed
```

Do not silently skip permission failures.

The overall scan must continue even when one scanner fails.

---

## 26. Fault Isolation

A failure in one scanner must not terminate the entire scan.

A scanner failure must be recorded and the remaining scanners must continue.

At the end:

```text
Scan Summary

Successful: 17
Partial: 3
Failed: 1
Skipped: 0
```

---

## 27. Scan Profiles

Implement:

```text
Quick
Standard
Deep
Migration
```

### Quick

- OS
- Services
- Processes
- Ports
- Basic software

### Standard

Quick +

- IIS
- Runtime
- Scheduled Tasks
- Packages
- Docker
- Certificates

### Deep

Standard +

- COM
- DLL
- Configuration
- Dependency analysis

### Migration

Deep +

- Risk analysis
- Dependency graph
- Migration checklist
- Evidence collection
- Configuration inventory
- External dependency analysis

---

## 28. CLI

The first user interface must be CLI.

Examples:

```bash
server-discovery scan
server-discovery scan --profile quick
server-discovery scan --profile standard
server-discovery scan --profile deep
server-discovery scan --profile migration
server-discovery scan --output ./report
server-discovery scan --format json
server-discovery scan --format csv
server-discovery scan --format html
server-discovery version
server-discovery doctor
server-discovery list-scanners
```

Example migration scan:

```bash
server-discovery scan \
  --profile migration \
  --output ./migration-report \
  --format html,json,csv
```

---

## 29. HTML Report

HTML is a primary deliverable.

The report should contain:

1. Executive Summary
2. Server Information
3. Operating System
4. Services
5. IIS
6. Applications
7. Processes
8. Ports
9. Installed Software
10. Runtimes
11. SDKs
12. COM Components
13. DLLs
14. Scheduled Tasks
15. Containers
16. Databases
17. Certificates
18. Dependencies
19. Dependency Graph
20. Migration Risks
21. Evidence
22. Scanner Errors
23. Migration Checklist

The report must be usable by an engineer without requiring the original tool.

---

## 30. JSON Schema

JSON is the canonical machine-readable output.

The schema must be versioned.

Example:

```json
{
  "schemaVersion": "1.0",
  "scan": {
    "startedAt": "...",
    "completedAt": "...",
    "profile": "migration"
  },
  "server": {},
  "services": [],
  "applications": [],
  "processes": [],
  "ports": [],
  "software": [],
  "runtimes": [],
  "comComponents": [],
  "scheduledTasks": [],
  "containers": [],
  "certificates": [],
  "dependencies": [],
  "risks": [],
  "evidence": [],
  "errors": []
}
```

Avoid breaking changes without schema versioning.

---

## 31. Testing Strategy

Every scanner must have tests.

Use:

- Unit Tests
- Integration Tests
- Fixture Tests
- Cross-platform Tests where practical

Do not depend on a production server for ordinary unit tests.

Use fixtures for:

- Registry
- IIS configuration
- systemd units
- package manager output
- command output
- process metadata

Test:

1. Normal discovery
2. Missing component
3. Permission denied
4. Invalid configuration
5. Command unavailable
6. Unexpected output
7. Architecture differences
8. Duplicate discovery
9. Conflicting evidence
10. Scanner failure isolation

---

## 32. Deduplication

Multiple scanners may discover the same component.

For example, Oracle Client may be found through:

- Registry
- File system
- PATH
- Process
- Installed software

These must be merged into one logical entity with multiple evidence records.

Do not produce duplicate logical components.

---

## 33. Correlation Engine

Implement correlation separately from discovery.

Example:

```text
Process
    ↓
Executable Path
    ↓
Windows Service
    ↓
Port
    ↓
Application
```

Correlation rules should be explicit and testable.

Avoid hidden heuristics inside scanners.

---

## 34. No Destructive Operations

The tool is an inspection tool.

It must NOT:

- Stop services
- Restart services
- Modify registry
- Modify IIS
- Modify systemd
- Delete files
- Install packages
- Change configuration
- Export private keys
- Change firewall rules

Default behavior is strictly read-only.

---

## 35. Security

The tool may normally run with elevated privileges.

Therefore:

- Minimize privilege where possible.
- Do not execute discovered binaries.
- Do not blindly execute configuration commands.
- Sanitize command arguments.
- Avoid shell injection.
- Do not expose secrets.
- Do not upload scan data anywhere by default.
- No telemetry by default.
- No external network dependency for core discovery.

---

## 36. Performance

A production server may contain hundreds of services and processes and many files/components.

Do not scan the entire filesystem indiscriminately.

Use:

- Targeted paths
- Configurable scan roots
- Parallel independent scanners
- CancellationToken
- Timeouts
- Caching where appropriate

Do not sacrifice correctness for premature optimization.

---

## 37. Logging

Use structured logging.

Every scanner should log:

```text
ScannerStarted
ScannerCompleted
ScannerFailed
ScannerSkipped
PermissionDenied
DiscoveryCount
```

Avoid logging secrets.

---

## 38. Extensibility

The architecture must make adding scanners easy.

Adding future scanners such as:

```text
RedisScanner
OracleScanner
SQLServerScanner
NginxScanner
KubernetesScanner
ApacheScanner
```

should not require extensive changes to Core.

Use interfaces such as:

```csharp
public interface IDiscoveryScanner
{
    string Id { get; }

    PlatformSupport PlatformSupport { get; }

    Task<DiscoveryResult> ScanAsync(
        DiscoveryContext context,
        CancellationToken cancellationToken);
}
```

The exact API may be adapted to repository conventions.

---

## 39. Scanner Registry

Scanners should be registered through a registry.

The CLI should support:

```bash
server-discovery list-scanners
```

and list platform support, profile support, and availability.

---

## 40. Migration Checklist Generation

The migration profile should automatically generate a checklist.

Example:

```text
[ ] Install IIS
[ ] Recreate IIS Sites
[ ] Recreate Application Pools
[ ] Install .NET Framework 4.8
[ ] Install Oracle Client 19c
[ ] Register COM component
[ ] Copy ERPService.exe
[ ] Install ERPService Windows Service
[ ] Open TCP 8011
[ ] Install SQL Server client
[ ] Import certificates
[ ] Recreate Scheduled Tasks
[ ] Configure environment variables
[ ] Configure external API endpoints
[ ] Validate database connectivity
[ ] Validate IIS application
[ ] Validate background service
```

Every checklist item should reference the discovered evidence that caused it to be generated.

---

## 41. Migration Readiness Score

Implement an explainable migration readiness score.

Example:

```text
Migration Readiness: 68 / 100
```

Factors may include:

- Known dependencies
- Unknown dependencies
- Critical components
- Legacy runtimes
- COM dependencies
- Native DLL dependencies
- External dependencies
- Certificate status
- Configuration completeness
- Permission limitations

Never produce an unexplained magic number.

The report must show how the score was calculated.

---

## 42. Unknown Dependency Detection

Identify:

- Unknown Port
- Unknown Process
- Unknown Service
- Unknown DLL
- Unknown COM Component
- Unknown External Endpoint
- Unknown Configuration Dependency

Example:

```text
Port 7777
Owner: unknown
Process: unknown
Risk: HIGH

Action:
Investigate manually before migration.
```

Preserving uncertainty is preferable to inventing an answer.

---

## 43. External Dependency Detection

Where technically feasible, detect references to:

- Database
- SMTP
- HTTP API
- HTTPS API
- LDAP
- Active Directory
- File Share
- UNC path
- Network Storage
- External Service

Do not actively probe external systems by default.

Static/configuration discovery is preferred.

---

## 44. Configuration Discovery

Discover relevant configuration files without exposing secrets.

Windows examples:

```text
web.config
app.config
*.json
*.config
```

Linux examples:

```text
/etc/*.conf
/etc/<application>/*
/opt/<application>/*
```

Capture:

```text
File path
Format
Detected sections
Detected dependency references
Secret presence
```

Do not dump full configuration files into the report.

---

## 45. Database Detection

Detect locally installed databases where possible:

```text
SQL Server
PostgreSQL
MySQL
MariaDB
Oracle
Redis
MongoDB
```

Distinguish:

```text
Installed
Running
Listening
Referenced by application
```

Do not connect to databases using discovered credentials.

---

## 46. Output Directory

A typical migration scan should produce:

```text
migration-report/
├── report.html
├── inventory.json
├── inventory.csv
├── dependency-graph.json
├── migration-checklist.md
├── risks.json
└── metadata.json
```

Do not place secrets in these files.

---

## 47. Development Process

The implementation must proceed incrementally.

DO NOT attempt to implement the entire system in one pass.

### Phase 0 — Repository Assessment

Before coding:

- Inspect repository
- Inspect existing architecture
- Inspect build system
- Inspect tests
- Identify target framework
- Identify existing conventions
- Identify reusable infrastructure

Produce:

```text
ARCHITECTURE.md
IMPLEMENTATION_PLAN.md
```

Do not begin major implementation until the architecture is understood.

### Phase 1 — Core Domain

Implement:

- Domain models
- Evidence model
- Scanner interfaces
- Discovery context
- Result model
- Error model
- Confidence model

Tests required.

### Phase 2 — Common Infrastructure

Implement:

- Process abstraction
- File abstraction
- Command execution abstraction
- Logging
- Cancellation
- Timeout handling
- Permission/error abstraction

Tests required.

### Phase 3 — Windows Foundation

Implement:

- OS scanner
- Process scanner
- Port scanner
- Windows service scanner
- Installed software scanner

Do not implement IIS/COM yet.

### Phase 4 — Windows Enterprise Components

Implement:

- IIS scanner
- Application Pool scanner
- COM scanner
- Scheduled Task scanner
- Certificate scanner
- Runtime scanner

### Phase 5 — Dependency Correlation

Implement:

- Process ↔ Service
- Process ↔ Port
- IIS ↔ AppPool
- IIS ↔ Application
- Application ↔ Runtime
- Application ↔ DLL
- Application ↔ COM
- Application ↔ Configuration

### Phase 6 — Linux

Implement:

- systemd
- process
- ports
- packages
- runtimes
- cron
- Docker

### Phase 7 — Analysis

Implement:

- Dependency graph
- Confidence
- Unknown dependency detection
- Migration risk
- Migration readiness

### Phase 8 — Reporting

Implement:

- JSON
- CSV
- HTML
- Dependency graph
- Migration checklist

### Phase 9 — CLI

Implement:

```text
scan
version
doctor
list-scanners
```

Profiles:

```text
quick
standard
deep
migration
```

### Phase 10 — Hardening

Perform:

- Security review
- Secret redaction review
- Permission testing
- Large-server performance testing
- Windows Server testing
- Linux distribution testing
- Failure isolation testing

---

## 48. Documentation Requirements

Maintain:

```text
README.md
ARCHITECTURE.md
IMPLEMENTATION_PLAN.md
SECURITY.md
SCANNERS.md
MIGRATION.md
PROGRESS.md
CHANGELOG.md
```

Every completed scanner must be documented.

For each scanner document:

- Purpose
- Supported Platforms
- Data Sources
- Permissions
- Entities Produced
- Evidence Produced
- Known Limitations
- Security Considerations
- Tests

---

## 49. Definition of Done

A feature is NOT complete merely because code compiles.

A scanner is complete only when:

- Implementation exists.
- Interface is correct.
- Unit tests exist.
- Error handling exists.
- Permission failures are handled.
- Evidence is generated.
- Duplicate detection is handled.
- Documentation exists.
- JSON serialization works.
- HTML reporting includes the data.
- Correlation rules are implemented where applicable.
- Build succeeds.
- Tests pass.

---

## 50. Engineering Rules

Always:

1. Inspect before modifying.
2. Prefer existing project conventions.
3. Keep platform-specific code isolated.
4. Keep discovery separate from correlation.
5. Keep correlation separate from analysis.
6. Keep analysis separate from reporting.
7. Treat evidence as first-class data.
8. Preserve uncertainty.
9. Never expose secrets.
10. Never perform destructive operations.
11. Write tests with each feature.
12. Update PROGRESS.md after each completed phase.
13. Update CHANGELOG.md for user-visible changes.
14. Keep the build clean.
15. Do not leave temporary files or abandoned implementations.

Never:

- Hard-code server-specific assumptions.
- Assume a component is used because it is installed.
- Assume a port identifies a service without process evidence.
- Copy secrets into reports.
- Execute unknown discovered binaries.
- Modify server configuration.
- Stop/restart services.
- Introduce unnecessary dependencies.
- Build a GUI before the discovery engine is stable.

---

## 51. Priority Order

When trade-offs are necessary, prioritize:

1. Correctness
2. Evidence / traceability
3. Security
4. Reliability
5. Cross-platform architecture
6. Testability
7. Performance
8. Reporting quality
9. User interface

Do not sacrifice correctness for visual polish.

---

## 52. Final Product Vision

The final product should allow an engineer to run:

```bash
server-discovery scan --profile migration
```

on an undocumented server and receive:

```text
SERVER MIGRATION ASSESSMENT
============================

Server:
ERP-SERVER-01

OS:
Windows Server 2022

Applications:
12

Windows Services:
37

IIS Sites:
8

Listening Ports:
14

Installed Software:
52

Runtimes:
9

COM Components:
126

Scheduled Tasks:
21

Containers:
6

External Dependencies:
18

Unknown Dependencies:
4

Critical Risks:
3

High Risks:
7

Migration Readiness:
68 / 100
```

and inspect an application dependency tree such as:

```text
ERP Web
│
├── IIS
├── AppPool: ERPAppPool
├── .NET Framework 4.8
├── Oracle Client 19c
├── COM: Acme.PdfGenerator
├── DLL: Vendor.Native.x86.dll
├── SQL Server
├── Certificate: erp.company.com
└── External API: api.company.com
```

The ultimate goal is:

> Transform an undocumented server into an evidence-backed, structured, dependency-aware migration inventory.

The tool must help an engineer answer:

1. What is installed?
2. What is running?
3. What is actually being used?
4. What application uses it?
5. Where is it located?
6. What depends on it?
7. What external systems does it depend on?
8. What permissions/configuration are required?
9. What could break during migration?
10. What must be recreated on the destination server?
