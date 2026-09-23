# Roadmap

## Foundation
- Establish Workspace Control identity and contracts.
- Replace bootstrapper-centric architecture with a permanent control-center model.
- Establish WinUI 3 desktop target.
- Separate domain, application, infrastructure and providers.
- Preserve proven inventory, cache, verification and operation concepts.
- Remove obsolete bootstrapper/job/WPF assumptions.

## Phase 1 — Local control center
- Modern WinUI 3 shell.
- Application inventory.
- Install/update/uninstall.
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

## Phase 4 — Profiles and extensibility
- Desired-state profile editor.
- Conditions and heterogeneous-machine support.
- Import/export.
- Plugin SDK boundary and trust model.

## Phase 5 — Optional cloud/fleet
- Accounts and synchronized profiles.
- Machine groups and per-machine overrides.
- Fleet inventory.
- Remote orchestration.
- Reports and alerts.

## 1.0 quality bar
Reliable, understandable, safe, tested on real Windows 11 machines, recoverable, maintainable and accessible.