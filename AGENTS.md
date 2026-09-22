# Dev Environment — Agent Instructions

## Product identity

- Product-facing name: Dev Environment.
- Canonical technical identity: DevEnvironment.
- Do not introduce new Bouna/Nabouna product identifiers in application code, paths or user-facing text.
- Primary Windows application/state root: C:\Dev\WorkspaceBootstrap.
- Persistent installer cache: C:\DevCache.
- GitHub ownership is infrastructure, not product identity.

## Strategic architecture

- C#/.NET 10 is the native Windows product and orchestration layer.
- The native engine is the single owner of provisioning, installer resolution, cache, verification, profiles, operation state, diagnostics and Windows integration.
- The WPF desktop application and self-contained CLI consume the same engine.
- The CLI is self-contained and single-file for Windows x64 so a fresh Windows installation does not require PowerShell 7 or a pre-installed .NET runtime.
- PowerShell is not a product dependency, bootstrap layer, execution backend or compatibility backend. Do not add new PowerShell.
- C++/Rust are reserved for a concrete native requirement that cannot be implemented cleanly in .NET.
- WinGet is an explicit package-manager fallback only where the component declares it; an official vendor source takes precedence.

## Engineering rules

- Prefer root-cause fixes over workarounds.
- Never suppress warnings/errors merely to make validation green.
- Keep installers idempotent and safe to re-run.
- Download to unique staging paths, validate, then atomically promote into the cache.
- Verify SHA-256 for every cached artifact and validate Authenticode for signed EXE/MSI artifacts.
- Keep older verified versions for rollback/reinstall.
- Do not store primary application state in C:\ProgramData or %LOCALAPPDATA%.
- Never persist secrets or registration tokens.
- Version checks must be component-specific.
- Failed staging must be cleaned without deleting valid cache entries.
- Destructive or irreversible actions require explicit confirmation.
- Optimization changes must have a rollback story where they are declared reversible.

## Git workflow

- GitHub is the source of truth.
- Work on feature branches and use PRs.
- CI runs on GitHub-hosted standard runners; the repository must not depend on a personal/self-hosted runner.
- The assistant may merge when the exact PR head is validated and coherent.
- Never claim CI green without checking the exact commit.

## Documentation

Architecture, installation ownership, paths, cache semantics, recovery, update behavior and durable product direction must be documented in the same change.

## Execution-first

When the user authorizes implementation with continue, vas-y, feu vert or equivalent, execute the repository changes before reporting them.

## Current migration rule

The project is migrating from the historical PowerShell implementation to the native .NET engine. This is a replacement, not a permanent dual-engine architecture.

Migration order:
1. native engine foundation;
2. official-source/cache implementation;
3. provisioning profiles and operation recovery;
4. baseline, diagnostics, maintenance and optimization;
5. desktop integration;
6. self-contained publish and fresh-Windows smoke validation;
7. remove every remaining .ps1/PowerShell product and test dependency;
8. update all docs and CI;
9. only then merge the migration.

Do not leave a PowerShell compatibility layer behind once native parity is verified.
