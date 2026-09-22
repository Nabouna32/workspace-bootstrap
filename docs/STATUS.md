# Status

## Product direction
The product is now **Workspace Control**, a permanent Windows 11 control center. The previous Workspace Bootstrap implementation is being migrated rather than extended indefinitely.

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
- GitHub-hosted CI only; self-hosted runners are abandoned.

## Existing foundations worth migrating
- inventory providers and evidence/ownership;
- installer verification;
- cache staging and atomic promotion;
- durable operations and recovery;
- CLI contracts;
- optimization scaffolding;
- profile/configuration manifests.

These are reusable foundations, not proof that the current architecture is final.

## Migration order
1. Documentation and architectural contracts.
2. New domain/application boundaries.
3. WinUI 3 shell.
4. Migrate inventory, software, cache and operations.
5. Remove legacy WPF/WorkspaceBootstrap desktop code.
6. Expand Windows administration, optimization, drivers and WSL.
7. Build desired-state profiles.
8. Add plugin boundary.
9. Add broad Windows integration/E2E validation.