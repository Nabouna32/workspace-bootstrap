# Workspace Control — Status

> This document is a concise operational snapshot. Git history remains the source of historical detail.

## Current state

The repository contains a functioning WinUI 3 desktop foundation, shared application/domain layers, Windows infrastructure and CLI.

The first **My Workspace** editor is functional for application desired state: users can work with user-owned Workspaces, search/select applications and persist the desired state under the portable application root.

The provisioning lifecycle includes persisted planning, confirmation boundaries, precondition checks and final verification.

Software inventory aggregation preserves provider isolation, provenance/evidence and partial-failure diagnostics.

The published Windows validation workflow builds and smoke-tests a self-contained x64 package. Real interactive Windows 11 visual, keyboard, accessibility, localization and DPI validation still requires a human on Windows.

## Current product gap

The product experience is not complete.

Remaining major surfaces include:

- richer Home / control-center dashboard;
- first-class Applications catalogue and management actions;
- broader Windows administration;
- semantic state presentation across domains;
- Optimizations;
- Drivers;
- WSL;
- Diagnostics;
- Cleanup;
- richer desired-state diff/risk/reversibility UX.

## Important constraints

- Windows 11 only.
- Portable, self-contained, unpackaged desktop.
- No hidden AppData persistence for application-owned state.
- Interactive mutations require explicit confirmation.
- GitHub-hosted CI only.
- English and French localization.
- Product direction comes from canonical documentation; status never overrides it.

## Next work

Prioritize product-experience completion over expanding infrastructure breadth. Keep the My Workspace model coherent while adding first-class domain surfaces and real Windows 11 validation.
