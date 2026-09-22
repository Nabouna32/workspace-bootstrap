# Architecture — Dev Environment

The product is a reproducible Windows 11 + WSL2 environment manager, not a collection of one-shot installation scripts.

## Product direction

The architecture is intentionally extensible rather than frozen around today's component list. The user defines product goals and constraints; technical leadership may proactively recommend and implement low-risk improvements to architecture, tooling, security, developer experience and product UX.

Any major directional change should be recorded with its rationale and trade-offs. Routine reversible technical choices do not require a separate approval step.

## Product identity and naming

- Product-facing name: **Dev Environment**.
- Canonical technical identity: **DevEnvironment**.
- GitHub repository ownership is an infrastructure detail and is not part of the product namespace, AppData path or user-facing identity. Public projects may live under the personal GitHub account after the organization migration.
- Do not introduce new `Bouna*` or `Nabouna*` product identifiers.
- Canonical Windows per-user data root: `%LOCALAPPDATA%\\DevEnvironment`.
- Historical names/paths must be migrated deliberately and never cause user-data loss.

## Strategic execution architecture

- **C#/.NET is the strategic application and orchestration language.**
- WPF remains the current Windows-native presentation layer.
- C# owns application/domain/job/execution orchestration; Windows APIs, SQLite, Registry, package managers, WSL and external tools are infrastructure boundaries.
- PowerShell is a transitional compatibility/execution backend. Do not deepen accidental PowerShell coupling when a maintainable C# implementation is appropriate.
- Migrate incrementally behind explicit interfaces; do not perform a big-bang rewrite or duplicate installer implementations.
- C++ is reserved for concrete native requirements such as drivers, native ABI dependencies or capabilities unavailable through .NET.
- Rust is a valid option for isolated memory-safe native/system helpers, but is not the strategic replacement for the C# application core.
- Introducing C++ or Rust requires a concrete technical requirement and a stable boundary, not a language preference.
- Current strategic decision: **C#/.NET first; native code only at justified boundaries**.

### C# versus native alternatives

C#/.NET is the best overall fit for the current Windows-first product workload: WPF, SQLite, Windows APIs, Registry, process/CLI orchestration, JSON contracts, asynchronous jobs, recovery and package-manager integration. Rust and C++ remain valid for isolated native requirements, but neither justifies replacing the application core.

### Migration target

```text
DevEnvironment.App (WPF)
        ↓
DevEnvironment.Application
        ↓
DevEnvironment.Domain
        ↓
DevEnvironment.Execution
        ↓
DevEnvironment.Infrastructure
        ├── Windows APIs / Registry / filesystem
        ├── WinGet / MSI / AppX / external tools
        ├── WSL bridge
        └── PowerShell compatibility backend
```

PowerShell capabilities migrate one responsibility at a time while preserving the persistent job, capability, recovery and audit model.

## Layers

1. **Desktop application** — WPF/MVVM presentation, localization and user interaction.
2. **Job orchestrator** — persistent C# queue, dependency/capability resolution, scheduling, recovery and resource locking.
3. **Typed engine contract** — versioned JSON boundary between desktop orchestration and system engine.
4. **PowerShell engine** — Windows administration, provisioning, maintenance and optimization execution.
5. **Declarative catalog** — component manifests and profile manifests.
6. **State engine** — checks, capability detection and explicit component states.
7. **Validation** — GitHub-hosted CI for public repositories.
8. **Local development** — Windows-first, with a minimal WSL environment for Linux-specific needs.

Repository CI runs on GitHub-hosted standard runners. The local PC remains the development and build workstation.

## Job orchestration

Long-running work is represented as persistent jobs. Jobs declare required and provided capabilities rather than hard-coding dependencies on the historical result of another job.

For example:

```text
wsl.install
  provides -> wsl.available

ubuntu.install
  requires -> wsl.available
```

A reboot-required result keeps the capability pending. The dependent job remains blocked until the scheduler verifies the capability after reboot. Independent jobs may continue when resource locks allow it.

The job queue is persisted in SQLite so application close, crash and Windows restart do not erase pending work. Startup recovery re-evaluates machine capabilities and resumes eligible jobs.

See `docs/JOBS.md` for the detailed contract and migration strategy.

## Ownership

Windows is the primary local development environment. It owns Visual Studio Build Tools, MSVC, MSBuild, CMake integration, Windows SDK, Flutter Windows and the optional Android/JDK/web toolchains used for local builds and tests.

WSL is a complementary Linux environment. It owns only Linux-first tooling and project-specific Linux dependencies. Toolchains are not duplicated merely to mirror GitHub Actions. A component belongs in WSL when Linux semantics or Linux-only tooling materially require it.

Android is no longer WSL-only. Local Android development may run on Windows; GitHub-hosted Linux runners provide the reproducible CI environment.

## Profiles


- Base: general PC software.
- Development: core web/Flutter/native development.
- Development Extended: additional languages, IDEs, LLVM and Java 21 for broader projects including Minecraft modding.
- Gaming: games and launchers; performance tuning lives in the shared optimization engine.

## Declarative rule

Profiles only select component IDs. Installation logic belongs to components. Dependencies are resolved centrally. Checks are reusable and return a defined state.

## PowerShell / UI language boundary

PowerShell engine and operator-facing machine-readable output use English stable terminology. The WPF layer owns localization and presentation labels, including French, English and future system-language support. Persisted job state uses stable language-neutral IDs/tokens rather than localized text.

## Maintenance and optimization

Maintenance and optimization are engines, not components pretending to be installers. This keeps system actions reusable.

Maintenance respects ownership boundaries: Windows-owned tools are checked on Windows, while Node, Java, Android/ADB, Flutter, Docker and KVM are checked through the WSL distribution that owns them.

Optimization actions are catalogued with level, impact, risk and reversibility metadata. The user can preview the plan before applying it. Reversible actions capture their previous state and guard rollback against later manual changes. Aggressive AppX removal uses a fixed allowlist and explicit confirmation; Microsoft Store is excluded.

## Online provisioning

No custom artifact cache exists. Network downloads are performed when needed. Temporary staging is used only to make archive installation transactional, then removed.

The environment intentionally supports both remote CI and local validation. GitHub-hosted CI is the source of truth for automated validation. Local toolchains exist as a convenience for rapid iteration and for avoiding repeated downloads of large APK/AAB/EXE artifacts when the developer wants to test a build on the local machine.

## Future extensibility


The same architecture can grow to Minecraft/modding, additional languages, C/C++, game development, device/emulator testing and project-specific CI without turning profiles into imperative scripts.


## Portable configuration and bootstrap

The desktop application is evolving toward a portable desired-state configuration. Local JSON import/export is the first transport. A future authenticated account may synchronize encrypted configuration and non-secret preferences, but it does not become authoritative for actual machine state; local capability detection remains authoritative.

Import is preview-first and generates independent persistent jobs rather than one monolithic operation. Machine-specific facts are re-detected on the target PC, and secrets are excluded from ordinary JSON by default.

See `docs/CONFIGURATION.md`.


## Durable product decisions captured during implementation

- The application is designed as a real maintenance/bootstrap product, not a thin wrapper around scripts.
- The queue is the long-term orchestration model: concrete work is represented by persistent independent jobs, while capabilities express semantic prerequisites.
- A job may be blocked without being failed; the scheduler must explain the reason and re-evaluate after reboot or capability changes.
- SQLite is the authoritative local queue/state store. Installer logs remain separate and are correlated through operation/run identifiers.
- Local JSON is the first-class portable configuration format. A future online account is optional synchronization/backup, not the source of truth for machine state.
- Version selection is explicit (latest, exact, minimum, range, channel); exact requests cannot silently downgrade or substitute another version.
- Network controls distinguish pause, admission control and true bandwidth throttling. Exact throttling is only promised for download paths controlled by the application.
- The architecture must remain observable and recoverable: crashes, reboots and interrupted work are states to reconcile, not reasons to hide errors.
- Product ideas discussed during development are either implemented immediately or captured in docs/ROADMAP.md under the durable product backlog.


## Installation, update, removal and debloat

The desired-state model is deliberately **bidirectional**: the product must eventually be able to converge a machine toward an installed state and toward an explicitly requested removed state. Installation, update and removal are different actions, but they share the same persistent job, capability, resource-lock, recovery and audit infrastructure.

Removal has two distinct product concepts:

- **Uninstall** — remove a known application/package/component using its authoritative uninstall mechanism. The operation is scoped to a concrete package and must show version, source and ownership when known.
- **Debloat** — remove optional software/components according to an explicit policy. Debloat is policy-driven and potentially destructive; it must never silently remove capabilities required by the user's desired configuration.

A removal job must not infer ownership merely from a package name. The inventory should identify whether software is Windows-owned, product-managed, installed by WinGet/MSI/AppX or another supported mechanism, or unknown/manual. Unknown ownership requires an explicit action path rather than an unsafe generic delete.

Before destructive removal, the application should provide a plan showing:

1. what will be removed;
2. current version and source when known;
3. scope (user/system);
4. capabilities that will disappear;
5. other desired-state entries that would become unsatisfied;
6. reboot requirement;
7. reversibility/reinstall path;
8. risk and impact.

Removal results remain persistent jobs. A failed uninstall, reboot-required uninstall or interrupted debloat operation is therefore observable and recoverable like any other job. If the component remains present in the desired configuration, the system must be able to explain the conflict rather than oscillating silently between install and remove.

The product should prefer official package-manager/uninstaller mechanisms (for example WinGet, MSI or AppX) instead of maintaining custom deletion logic. Aggressive debloat remains an explicit allowlist/policy feature, with protected core components and clear confirmation.


## Inventory and installed-state intelligence

The product needs a first-class **inventory engine** to know what is actually present before it decides what to install, update or remove. Inventory is observation; desired configuration is intent; usage evidence is a separate signal. These three concepts must not be collapsed into one state.

### Provider model

Inventory is composed from independent providers. Initial Windows providers should cover supported uninstall registry entries and WinGet, with MSI/AppX and specialized providers added where they provide authoritative metadata. Future WSL inventory runs inside the owning distribution for WSL-owned toolchains.

Providers return normalized facts plus provenance. The canonical inventory layer deduplicates observations without throwing away their source. It must be honest about coverage: the UI should say **supported/known inventory sources**, not claim that every file or executable on Windows has been discovered.

### Canonical inventory fact

An inventory item should retain, where available:

- stable component/package identity;
- display name and version;
- provider and provider-specific identity;
- user/system/WSL scope;
- install location;
- ownership classification (system, product-managed, package-manager-managed, manual, unknown);
- capabilities provided;
- detection timestamp;
- evidence used for usage/dependency analysis.

Inventory scans are read-only. Mutation decisions must revalidate the live machine immediately before destructive work because inventory snapshots can become stale.

### Version families and old-version recommendations

Versioned families must preserve **all detected versions**, not just the newest one. Java/JDK is a representative case: JDK 21 and JDK 17 may legitimately coexist because different toolchains can require different runtimes. An older version is therefore a recommendation signal, not proof that removal is safe.

A cleanup candidate should normally require all of the following:

1. a newer compatible version exists;
2. no reliable usage/dependency evidence requires the older version;
3. desired configuration does not require the older version;
4. ownership is known sufficiently to select the correct uninstall mechanism;
5. an authoritative removal mechanism exists.

The UI should explain the evidence behind a recommendation (for example active `JAVA_HOME`, PATH resolution, known toolchain configuration, desired version policy, or lack thereof) and should never silently remove an older version.

### Desired / actual / usage

```
Desired configuration
        |
        v
  Desired-vs-actual diff <---- Machine inventory
        |                         |
   install/update              usage evidence
        |                         |
        +----------+--------------+
                   v
             Cleanup analysis
                   |
            approved removal plan
                   |
          persistent package.remove job
                   |
             postcondition scan
```

A component can be installed but unused, installed and required, active, outdated, orphaned, or unknown. These are observations/signals, not interchangeable lifecycle states.

### Removal integration

Inventory does not delete anything. An approved cleanup plan produces normal persistent removal jobs. Removal jobs use the same capability, resource-lock, recovery, audit and verification infrastructure as installation/update jobs. After removal, inventory is refreshed and the expected absence is verified before dependents are recalculated.

Windows and WSL ownership boundaries remain explicit. For example, the Windows inventory must not decide that a WSL-owned Java or Android SDK should be removed merely because it is not visible to a Windows provider.
