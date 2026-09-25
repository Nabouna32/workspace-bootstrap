# Workspace Control — Roadmap

This roadmap is directional. It follows the intended product direction and may require substantial implementation replacement.

## Phase 0 — Realignment

- Reconcile brainstorming, product vision, UX and architecture into one coherent target.
- Audit the existing implementation against that target.
- Identify reusable code versus code that preserves the wrong product model.
- Remove historical bootstrap/provisioning constraints where they no longer serve the product.
- Establish the intended first-run and My Workspace experience before expanding feature breadth.

## Product foundation

- Permanent Windows control-center experience.
- First-run observation of the real machine.
- User-created Workspaces as the primary model.
- Applications as one major domain, not the product itself.
- Clear desired-state, diff, plan, confirmation and verification lifecycle.
- Provider/capability architecture independent of WinGet.
- Portable/local-first operation.
- Localization, accessibility, themes and semantic states from the foundation.

## Product domains

- Applications and software management.
- Windows administration and policies.
- Optimizations.
- Cleanup.
- Diagnostics.
- Drivers.
- WSL.
- Inventory.
- Cache and offline capabilities.
- Workspaces, conditions and desired-state management.

Domains should be added through coherent product surfaces, not isolated technical pages.

## Desired-state maturity

- Rich diff, impact, risk and reversibility.
- Conditions and declarative rules.
- Import/export.
- Recovery and rollback where technically possible.
- Operation history and diagnostics.
- Controlled plugin/provider extensibility.

## Future

- Cloud synchronization.
- Machine groups and fleet management.
- Remote orchestration.
- Reports and alerts.

## Quality bar

The product is not complete because an engine or page exists. It must be understandable, pleasant, safe, recoverable, accessible, localized and genuinely validated on Windows 11.
