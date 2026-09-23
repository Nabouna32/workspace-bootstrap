# Status

## Product direction
The product is **Workspace Control**, a permanent Windows 11 control center. The migration from the historical Workspace Bootstrap implementation is now structurally established and future work should extend the new architecture rather than revive retired layers.

## Confirmed decisions
- Windows 11 only.
- C#/.NET 10.
- WinUI 3 / Windows App SDK for desktop.
- Fluent-inspired modern UX with System/Light/Dark themes.
- Semantic status colors.
- Simple / Advanced / Expert presentation levels.
- Online-first installation; a verified local installer is reused when it already matches the requested current version. There is no cache-only/offline provisioning mode.
- Software, Windows administration, diagnostics, cleanup, optimization, drivers and WSL are product domains.
- Profiles represent desired state.
- Local import/export first; future cloud/fleet is optional.
- Providers/plugins are planned.
- Normal interactive mode never mutates without explicit confirmation.
- Elevated session is optional; least privilege remains the architecture.
- GitHub-hosted CI only; self-hosted runners are abandoned and must not be reintroduced.

## Current architecture state
- Native WinUI 3 shell exists under `src/WorkspaceControl.Desktop`.
- Domain models and Application contracts are separated from Windows implementation.
- `WorkspaceControl.Infrastructure` is a real project with its own physical source tree; the retired `WorkspaceBootstrap.Engine` project has been removed.
- Desktop and CLI consume the Application contract through an explicit Infrastructure composition root; the old `EngineFacade` service-locator layer is removed.
- Infrastructure namespaces are aligned with `WorkspaceControl.Infrastructure`.
- Software inventory aggregation has provider isolation, deterministic merge behavior, provenance/evidence preservation and partial-failure diagnostics.
- Mutable cache/state/log data defaults to LocalAppData rather than the installation directory.
- Packaged configuration is resolved from the application content root.
- Legacy WPF desktop has been removed; WinUI 3 is the only desktop UI target.
- The desktop Profiles surface now exposes profile selection, persisted plan review, explicit confirmation, live apply progress, verification results, stale-plan handling and safe resume for resumable failures.

## Next engineering priorities
1. Add first-class Windows integration, published-artifact, UI/accessibility and security validation for the provisioning surface.
2. Strengthen software inventory/provider contracts and update semantics.
3. Replace remaining infrastructure composition with a testable dependency-injection/composition strategy where it materially improves lifetime management.
4. Complete Windows administration, policy, diagnostics, driver and WSL capability boundaries.
5. Build richer desired-state diff presentation and risk/reversibility metadata.
6. Harden verified installer reuse, artifact retention and integrity verification.
7. Add Windows integration, published-artifact, UI/accessibility and security validation.
7. Continue WinUI UX hardening, localization and runtime binding verification.
9. Remove remaining historical names/dead documentation as each boundary becomes authoritative.

## Reusable foundations
- inventory providers and evidence/ownership;
- installer verification;
- cache staging and atomic promotion;
- durable operations and recovery;
- CLI contracts;
- optimization scaffolding;
- profile/configuration manifests.

These foundations are reusable implementation starting points, not proof that every product capability is complete.
