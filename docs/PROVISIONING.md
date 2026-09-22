# Provisioning architecture

The environment is built from independent components and profiles.

- Components own installation and verification logic.
- Profiles select components; profiles do not contain installation logic.
- Dependencies are declared by components and resolved before installation.
- A persistent local installer cache is maintained under C:\\DevCache; it is not an artifact cache and is not a CI cache.
- The component's declared official vendor source is preferred for latest-version resolution and installer download. An explicit cache-only mode can restore a previously exported verified cache without network access.
- Cached official installers are SHA-256 verified before reuse; Authenticode signatures are also validated for EXE/MSI installers.
- WinGet is an explicit fallback only when no automated official source is declared.
- Cache entries are immutable by version/locale/architecture/source; a verified matching version is never downloaded again. Exported cache metadata uses relative installer paths so the cache can be restored on another drive or fresh Windows installation.
- Temporary staging is cleaned after each download.
- Windows and WSL have explicit ownership boundaries.
- Visual Studio Build Tools owns the Windows SDK and native C/C++ toolchain.
- The Maintenance and Optimization engines are separate from provisioning so they can evolve without duplicating component logic.

## Product direction and decision ownership

The user defines product goals, constraints and important personal preferences. Technical implementation is intentionally delegated to tech-lead judgment: the assistant may propose a better architecture, tooling choice, UX flow, security measure or extensibility mechanism when the evidence supports it.

When a proposal materially changes product scope, cost, reversibility or user experience, document the trade-off and surface it for the user's decision. Routine technical improvements may be implemented directly.

## Component state model

The engine uses explicit states:

- MISSING
- OUTDATED
- CURRENT
- INSTALLED
- CONFIG-INCOMPLETE
- REPAIRABLE
- FAILED

A local check is not falsely reported as "latest" when the component uses a latest-stable policy. Remote freshness remains an update operation.

## Profiles

Current product profiles:

- base
- development
- development-extended
- gaming

Maintenance and optimization are first-class engines, not fake installable components.

Development Extended targets future projects such as Minecraft modding and adds IDE, Python, Rust, Go, LLVM and JDK 21 capabilities.

## Updates

WinGet upgrades are previewed and confirmed interactively. Provisioning uses the local installer cache first when a verified installer is available; official-source metadata drives online refreshes. Flutter uses flutter upgrade. Windows Update is intentionally not silently automated: the Maintenance Center diagnoses its services and opens the official Windows Update settings.

## Maintenance Center

The interactive center provides:

- Windows Update diagnostics and service restart;
- DISM CheckHealth / ScanHealth / RestoreHealth;
- SFC;
- Component Store analysis and cleanup;
- temporary-file and Windows cleanup;
- Windows host health plus WSL-owned Node/Java/ADB/Flutter/Docker/KVM checks;
- network diagnostics;
- explicit Winsock reset;
- local diagnostic report generation.

## Optimization

Optimization is separate from provisioning and has three levels:

- Safe
- Advanced
- Aggressive

The user can preview the plan before applying it. Each reversible action carries impact/risk metadata and records its previous state under C:\ProgramData\BounaDevEnvironment\optimization. Registry rollback is guarded against values changed manually after optimization. Power-plan rollback is similarly guarded.

Aggressive AppX debloat is isolated behind an explicit allowlist and confirmation because AppX removal is not guaranteed to be automatically reversible.

## Dry-run / plan de provisioning

Le launcher Windows expose un mode lecture seule avec `-PlanOnly`. Il résout les dépendances, déduplique les composants entre les profils sélectionnés et construit un inventaire de session : chaque composant n'est contrôlé qu'une seule fois. Le dry-run ne lance aucun installateur et ne modifie pas le système, mais peut interroger WinGet pour vérifier les mises à jour.

Exemples :

    .\\bootstrap\\windows\\dev-env.ps1 -PlanOnly
    .\\bootstrap\\windows\\dev-env.ps1 -PlanOnly -Profile development

Le plan distingue notamment MISSING, OUTDATED, CONFIG-INCOMPLETE, REPAIRABLE, CURRENT et INSTALLED. Un composant INSTALLED n'est pas présenté comme CURRENT lorsque la fraîcheur distante n'a pas été vérifiée. Le dry-run est donc adapté à une première inspection d'un PC neuf avant le premier provisioning réel.

## CI and local development

Public repository validation uses GitHub-hosted standard runners. Do not provision a persistent GitHub Actions runner as part of this environment.

The local workstation still installs development toolchains when they are useful for fast local builds/tests. This is deliberately separate from CI: a local build can reduce repeated artifact downloads and provide immediate feedback, but GitHub-hosted CI remains the authoritative automated validation path.

## Persistent job orchestration


Provisioning is being migrated from a single long-running operation model to a persistent job model. Each concrete operation is represented as an independent job with stable IDs, required capabilities, provided capabilities and resource locks.

Jobs are persisted in SQLite and survive application close, crash and Windows restart. A job blocked by a missing capability is not failed; it becomes runnable when capability detection confirms that the prerequisite is satisfied.

A reboot-required step therefore produces a scheduler state such as `waiting-reboot`. After Windows restarts, capability detection runs again and dependent jobs are unblocked automatically when their prerequisites are actually available.

The existing PowerShell provisioning worker remains the execution engine during the migration. C# owns persistence, scheduling and recovery.


## Portable bootstrap configuration

A fresh Windows installation should be able to restore a previously defined desired environment without assuming that the target machine is identical to the source. The future importer validates a versioned JSON bundle, detects target capabilities, calculates desired-vs-actual state, previews the delta, enqueues independent jobs, and verifies every operation.

The configuration is declarative and does not copy blindly installed state, machine identifiers or hardware assumptions. Credentials are excluded from ordinary JSON and handled separately by a future secure mechanism.
