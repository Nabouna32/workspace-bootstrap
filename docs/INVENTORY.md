# Inventory Engine

## Purpose

The inventory engine is the read-only observation layer that answers what is actually installed and available on the machine. It feeds desired-state reconciliation, diagnostics, update discovery and cleanup planning. It never mutates the machine during a scan.

The product should claim coverage of supported and known installation sources, not literal discovery of every file or executable.

## Separation of concerns

- Inventory = observed machine facts.
- Desired state = what the user/configuration wants.
- Usage evidence = evidence that an installed component is active or required.

Installed does not mean unused, obsolete or safe to remove.

## Provider architecture

Providers are independent and return normalized facts plus provenance.

Initial Windows providers:
- Registry uninstall entries, including relevant 32-bit/64-bit views.
- WinGet installed package metadata.
- MSI/AppX providers where they add authoritative coverage.

Future providers include WSL, Java/JDK, .NET, Node, Python, Flutter, Android SDK, IDE/toolchain-specific providers and Windows components. WSL-owned components must be inventoried from the owning WSL side rather than treated as Windows packages.

## Canonical inventory item

Each normalized item should retain when available:
- stable identity and display name;
- installed version and version family;
- provider and provider-specific identity;
- source/package manager;
- user/system/WSL scope;
- install location;
- ownership classification: system, product-managed, package-manager-managed, manual or unknown;
- capabilities provided;
- detection timestamp;
- usage/dependency evidence.

Provider provenance survives deduplication. Registry and WinGet observations of the same product may become one logical item with multiple source observations.

## Version families

All detected versions in a family must be preserved. Multiple versions can be valid.

Example: JDK 21.0.8 can be active while JDK 17 remains required by another toolchain. Therefore the rule 'newer version exists => delete older version' is forbidden.

The same principle applies to .NET, Node, Python, Flutter and Android SDK packages.

## Usage and dependency evidence

Usage classification must be evidence-based. Useful evidence includes PATH resolution, JAVA_HOME and related environment variables, known toolchain configuration, explicitly enabled project configuration scanning, launcher configuration and capability relationships. Running processes are transient evidence and should not alone become durable dependency proof.

## Status signals

Suggested inventory signals:
- Installed
- Active
- Used / Required
- Older version
- Unused detected
- Orphan candidate
- Unknown

Signals can coexist. An item can be Installed + Older version + Unused detected.

## Cleanup recommendations

An old-version recommendation normally requires: a newer compatible version, no reliable usage/dependency evidence for the old version, no desired-state requirement, sufficiently known ownership, and an authoritative uninstall mechanism.

Every recommendation must explain its evidence and confidence. Recommendations are never automatic deletion.

## Removal integration

Inventory never removes software. The lifecycle is: scan, compare desired state and usage evidence, generate cleanup plan, show impact/capabilities/ownership/reboot/reversibility, obtain explicit approval, create persistent package.remove jobs, revalidate the target, execute through the authoritative uninstall mechanism, verify absence, refresh inventory and recalculate capabilities.

If desired configuration still requires a removed component, surface the conflict instead of silently oscillating between remove and reinstall.

## Safety rules

- Inventory scans are read-only.
- Never infer ownership from a display name alone.
- Never use directory deletion as a generic uninstall mechanism.
- Never treat an older version as automatically obsolete.
- Never remove a capability required by desired configuration without an explicit decision.
- Never mix Windows-owned and WSL-owned inventory.
- Revalidate before destructive work because snapshots become stale.
- Preserve provider, source and version provenance.

## Persistence

Inventory snapshots are observations, not authoritative desired state. A future SQLite inventory store may retain scan IDs, machine identity, provider observations, evidence, timestamps, recommendation decisions and job correlation IDs. The queue database remains authoritative for job lifecycle.

## Implementation order

1. C# canonical models and provider interfaces.
2. Provider command abstraction and parser tests.
3. Registry uninstall provider.
4. WinGet provider.
5. Normalization and deduplication.
6. Version-family grouping.
7. Evidence collection.
8. Cleanup recommendation engine.
9. WPF inventory and cleanup UI.
10. package.remove executor integration.
11. WSL inventory provider.
12. Inventory history and richer diagnostics.

The first providers must be safe to run locally without downloading or installing anything.

## Provider health and coverage

Inventory is intentionally **provider-based**, not a claim that the application can magically enumerate every file or every installer ever used on Windows.

Each provider reports its own health alongside observations. A provider failure is visible to the UI and does not discard successful observations from other providers.

Current Windows providers:

- Registry uninstall inventory: Add/Remove Programs data from HKLM/HKCU and both registry views.
- WinGet: the installed-application list exposed by `winget list`, including applications that WinGet can see even when they were not originally installed through WinGet.
- Future providers: AppX/MSIX, MSI-specific metadata, and toolchain-specific inventories.

WinGet is used here as an **observation provider**, not as the authoritative source for all installation provenance. Its `list` output can include software installed by other mechanisms, so ownership remains Unknown until stronger evidence exists.

For removal, the product will only execute an authoritative mechanism after an explicit preview/approval flow. WinGet supports uninstall by exact package ID and version, including applications that were not originally installed through WinGet, but that capability does not by itself prove ownership or safety.


### Cleanup recommendation gate

The current analyzer is deliberately conservative:

- side-by-side versions are preserved as separate inventory instances;
- version-family analysis marks older instances without deleting anything;
- a cleanup recommendation requires an explicit uninstall mechanism **and known non-system ownership**;
- unknown ownership is not enough to recommend removal;
- actual removal remains a separate `package.remove` job with preview, approval, revalidation and post-removal verification.

This means the inventory can show more software than it is currently willing to recommend removing. That is intentional: discovery coverage and destructive-action confidence are separate concerns.
