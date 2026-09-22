# Workspace Bootstrap — Agent Instructions

## Product identity
- Product-facing name: **Workspace Bootstrap**.
- Canonical technical identity: **WorkspaceBootstrap**.
- Windows-only product. Linux/WSL tooling is outside product scope.
- The application is designed to be portable: executable, bundled assets and local mutable state live with the application package.
- Installer cache is kept inside the application package under `cache`. 

## Strategic architecture
- C#/.NET 10 is the single application and orchestration technology.
- The native .NET engine owns provisioning, installer resolution, cache, verification, profiles, operation state, diagnostics, maintenance, optimization and Windows integration.
- The desktop application and self-contained CLI consume the same engine.
- The desktop presentation is a native Windows UI; WPF is the current presentation framework, not a second application architecture.
- There is one product architecture, not parallel implementations.
- PowerShell, batch files, shell scripts and WSL are not product dependencies or execution backends.
- WinGet is an explicit package-manager fallback only where a component declares it; official vendor sources take precedence.
- C++/Rust are reserved for a concrete native requirement that cannot be implemented cleanly in .NET.

## Engineering rules
- Prefer root-cause fixes over workarounds.
- Never suppress warnings/errors merely to make validation green.
- Keep installers idempotent and safe to re-run.
- Download to unique staging paths, validate, then atomically promote into the cache.
- Verify SHA-256 for cached artifacts and validate Authenticode for signed EXE/MSI artifacts.
- Keep older verified versions for rollback/reinstall.
- Never persist secrets or registration tokens.
- Version checks must be component-specific.
- Failed staging must be cleaned without deleting valid cache entries.
- Destructive or irreversible actions require explicit confirmation.
- Optimization changes must have a rollback story where declared reversible.
- The UI must remain presentation-focused; provisioning and system mutation belong to the Engine.

## Git workflow
- GitHub is the source of truth.
- Work on feature branches and use PRs.
- GitHub Actions uses standard GitHub-hosted runners.
- CI must not depend on a developer workstation.
- Never claim CI green without checking the exact commit.

## Execution-first
When the user authorizes implementation with `continue`, `vas-y`, `feu vert` or equivalent, execute repository changes before reporting them.

## Migration cleanup
The historical script/WSL product implementation is retired. Native .NET is the only product implementation.
Remove retired paths completely; do not keep compatibility layers, migration checks or documentation for them.
