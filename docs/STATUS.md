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
- Local-first and offline-capable.
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

## Next engineering priorities
1. Harden Application contracts and operation lifecycle.
2. Introduce provider-contract tests and strengthen software inventory provenance/update semantics.
3. Replace remaining infrastructure composition with a testable dependency-injection/composition strategy where it materially improves lifetime management.
4. Complete Windows administration, policy, diagnostics, driver and WSL capability boundaries.
5. Build first-class desired-state diff/plan/confirm/apply/postcondition flows.
6. Expand cache/offline retention, export/import and integrity verification.
7. Add Windows integration, published-artifact, UI/accessibility and security validation.
8. Continue WinUI UX hardening and runtime binding verification.
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
