# Workspace Control — Status

> Concise operational snapshot. Git history contains historical detail.

## Current state

The repository contains a functioning WinUI 3 desktop implementation, shared application/domain layers, Windows infrastructure and CLI.

This implementation is **not yet considered aligned with the intended product direction**. It is an implementation baseline that must be audited and, where necessary, rebuilt.

The current code contains substantial desired-state, inventory and provisioning work, including a first My Workspace editor and persisted operation lifecycle. These capabilities are useful evidence and reusable material, but their existence does not make the current UX, architecture or product sequencing authoritative.

## Product model being implemented

The current realignment establishes these product fundamentals:

- A Workspace is a user-owned **partial desired state**, not a list of applications.
- The user explicitly chooses which parts of the machine are managed.
- Undefined desired state means **no requirement and no inferred action**.
- Unknown observed state remains distinct from undefined state.
- A Workspace can be created through an explicit capture of the current machine, then saved, exported, imported and reused.
- This supports both everyday maintenance and rebuilding an environment after formatting.
- Optional templates can bootstrap a user-owned Workspace but do not replace it.
- The same model is intended to remain viable for future multi-machine and organizational deployment.

## Realignment focus

The next phase is to compare the actual implementation against the brainstorming and canonical product documents, then restore the intended direction.

Priority is:

1. establish the intended product model and first-use experience;
2. audit the current domain/application architecture against that model;
3. remove or replace abstractions that preserve the wrong direction;
4. rebuild the Windows 11 UX around the actual product vision;
5. reconnect existing reusable engine capabilities only where they serve that vision;
6. verify the resulting product on real Windows 11.

A full or partial reconstruction is acceptable if incremental repair would preserve the wrong architecture or UX.

- Application desired-state intent is now explicitly modeled as **undefined / present / absent**, with observed application state kept separate.
- The Workspace application editor now exposes **Don’t manage / Keep / Remove**, allowing debloat behavior to use the same desired-state pipeline as installation.
- Removal planning and execution use the existing review → confirmation → apply → verification lifecycle; this slice makes the user intent for removal explicit in the Workspace UI.
- Application removal execution is now capability-driven: confirmed plans persist the observed WinGet package identity/source/version instead of assuming the catalog is the uninstall mechanism; removal is blocked when no supported capability is observed.
## Current constraints

- Windows 11 x64.
- Local-first operation.
- English and French.
- Accessibility and semantic states are product requirements.
- No silent interactive mutation in normal interactive mode.
- Existing code is not a constraint on the target product.

## Known implementation debt

The implementation baseline has now been cleaned of the legacy bootstrap/provisioning runtime paths and public operation naming audited in this phase. Compatibility terminology remains in historical documentation and the component manifest schema, while the desktop navigation/UX still does not fully express the broader permanent control-center model described by the product documents.

The current Workspace UX is still too application-centric and too close to the earlier checklist/profile prototype. It must evolve toward explicit desired-state domains, current-vs-desired comparison, controlled capture and review-before-apply.

These remaining gaps are alignment work, not reasons to weaken the intended product direction.

## Recent implementation progress

- The Workspace page has been rebuilt around Workspace identity, managed scope, current-vs-desired comparison and explicit review before apply.
- Adding an application no longer creates a Workspace implicitly; the user must explicitly select or create one.
- The Home page is being reshaped into a Workspace-oriented control center rather than a collection of maintenance counters.
- The first executable Windows desired-state slice is now available: Windows appearance is user-scoped, reversible and represented through the existing plan/confirm/verify registry path. The user-facing choices are System, Light and Dark; System clears any explicit light/dark Workspace intent and does not force a mode.
- The Windows appearance mapping, including the System intent, is covered by domain-level tests.
- The UX now defines a shared semantic color grammar: green for success/healthy, blue for information/neutral action, amber/orange for warning/attention/risk, red for error/failure/blocked, and neutral for undefined/secondary information.

## Recent implementation progress

- The Workspace editor now supports an explicit first capture slice: create a new user-owned Workspace from applications detected as installed on the current PC; other settings remain undefined.

## Application removal UX progress

- The application editor now presents the application name, observed state and explicit **Don't manage / Keep / Remove** intent together.
- Diff and plan action/state codes are localized for the desktop UX instead of exposing internal codes as the primary labels.
- Review explicitly highlights when the confirmed plan contains application removals.
- Removal planning is covered for the already-absent and incomplete-inventory safety cases; incomplete evidence blocks removal rather than guessing.

## Verification

Published Windows validation exists, but automated launch is not a substitute for real interactive Windows 11 UX, accessibility, DPI and localization validation.
