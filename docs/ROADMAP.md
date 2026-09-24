# Roadmap

## Foundation
- Establish Workspace Control identity and contracts.
- Replace bootstrapper-centric architecture with a permanent control-center model.
- Establish WinUI 3 desktop target.
- Separate domain, application, infrastructure and providers.
- Preserve proven inventory, cache, verification and operation concepts.
- Remove obsolete bootstrapper/job/WPF assumptions.

## Phase 1 — Local control center
- Explicit provisioning lifecycle: observed state, desired state, persisted plan, confirmation, apply, post-condition verification and final-state verification.
- Safe recovery with persisted plans and stale-plan detection.
- Modern WinUI 3 shell.
- Application inventory.
- Install/update/uninstall through the shared desired-state/provisioning engine, with persisted removal operations, post-condition verification and rollback when exact restoration is available.
- Provider selection and interactive installers.
- Reuse already-downloaded verified installers when they match the requested current version.
- Diagnostics and operation history.
- Theme, localization and accessibility foundations.

## Phase 2 — Windows management
- Windows configuration inventory.
- Policy/GPO-oriented UI.
- Documented registry-backed settings.
- Services and scheduled tasks where appropriate.
- Optimization catalog with risk/reversibility.
- Conservative cleanup/residual analysis.

## Phase 3 — Hardware and WSL
- Driver inventory and update discovery.
- Hardware diagnostics.
- WSL installation/configuration.
- WSL-aware profiles.

## Phase 4 — Desired state and extensibility — foundation complete
- Versioned desired-state profile contract.
- Desired-state observation and diff engine.
- Conditions and heterogeneous-machine support.
- Import/export.
- Plugin SDK boundary and trust model.

The technical desired-state foundation is established. The user-facing Workspace editor remains a product-experience task and is not considered complete merely because the profile engine exists.

## Phase 5 — Product experience completion
- First-class Applications catalogue with search, categories, filters, rich state and actions.
- My Workspace builder with check/uncheck desired-state controls.
- Optional editable templates rather than mandatory predefined profiles.
- Home control-center dashboard.
- Updates, Windows, Optimizations, Diagnostics and Cleanup surfaces with consistent semantic states.
- Clear Unknown / Unavailable / Blocked / Error presentation.
- Consistent risk, reversibility and restart-impact presentation.
- Full light/dark/system visual polish, accessibility and English/French coverage across new surfaces.


## Phase 5 — Optional cloud/fleet
- Accounts and synchronized profiles.
- Machine groups and per-machine overrides.
- Fleet inventory.
- Remote orchestration.
- Reports and alerts.

## 1.0 quality bar
Reliable, understandable, safe, tested on real Windows 11 machines, recoverable, maintainable and accessible.
