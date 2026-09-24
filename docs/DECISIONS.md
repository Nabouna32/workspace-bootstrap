# Workspace Control — Decision Audit

This document records architectural decisions reconstructed from the repository, merged pull requests and current implementation. It is an engineering guardrail, not a transcript of private chat history.

## What was verified

The repository history contains an explicit portable-storage direction:

- **PR #1 — native C# Windows cutover:** the merged change explicitly described moving application state and installer cache into the portable application package.
- **PR #14 — provisioning hardening:** the merged change introduced isolated workspace roots for deterministic tests. That is a testability mechanism, not a product requirement for hidden per-user storage.
- **PR #19 — Workspace Control architecture:** the merged change moved mutable cache/state/log data out of the installation directory and made packaged configuration resolve from the application content root. This established a distinction between packaged read-only resources and mutable application data, but did not establish AppData as a product requirement.
- **PR #56 — profile import/export:** the merged change documented profiles as portable data and added safe import/export.
- **PR #81 — product-contract restoration:** the merged documentation established `docs/PRODUCT-EXPERIENCE.md` as the canonical product/UX contract and added anti-drift rules to `AGENTS.md`.
- **PR #82 — first My Workspace implementation:** the open change introduced LocalAppData persistence for user Workspaces. That choice is inconsistent with the earlier portable-storage direction and is therefore corrected by the current audit rather than treated as a new product decision.

## Current decision

The product contract is now explicit:

> **Workspace Control is portable. Application-owned mutable state must remain under the explicit portable application root. It must never silently move to AppData or another hidden per-user store.**

This includes Workspaces, application configuration/preferences, cache, operation state, optimization state and application-owned logs.

The only supported default root is `AppContext.BaseDirectory`; constructors may receive an explicit root for tests or controlled composition.

## Important distinction

“Portable” does **not** mean that every Windows resource is stored as a file beside the application.

Workspace Control may legitimately use Windows system stores when those stores are the **thing being managed**. For example, a registry-backed Workspace setting is a Windows configuration target. It must not be confused with using the Registry to store Workspace Control's own configuration.

Likewise, an explicit user-selected export destination is not hidden application persistence.

## What cannot be claimed from history

The repository does not contain a complete transcript of the long private product discussions that preceded every PR. Where those discussions are not preserved in repository artifacts, this audit does not invent them.

The current portable/no-hidden-AppData rule is therefore recorded explicitly here and in `AGENTS.md` so future implementation work does not depend on recovering conversational context.

## Anti-drift rule

When implementation reality conflicts with this document, the implementation is the part that must be corrected unless the product direction is deliberately changed.

A deliberate change to the portability model requires:

1. an explicit product/architecture decision;
2. updates to this document, `docs/PORTABILITY.md` and `AGENTS.md`;
3. an impact audit of configuration, Workspaces, cache, state, logs, packaging, uninstall/reinstall and backup behavior;
4. regression tests and CI guardrails;
5. a coherent PR description explaining the migration and user impact.