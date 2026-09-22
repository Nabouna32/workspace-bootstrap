# Workspace Bootstrap — Agent Instructions

## Product identity
- Product-facing name: **Workspace Bootstrap**.
- Canonical technical identity: **WorkspaceBootstrap**.
- Windows-only product. Linux/WSL tooling is outside product scope.
- Primary application/state root: `C:\Dev\WorkspaceBootstrap`.
- Persistent installer cache: `C:\DevCache`.

## Strategic architecture
- C#/.NET 10 is the single product and orchestration technology.
- The native .NET engine owns provisioning, installer resolution, cache, verification, profiles, operation state, diagnostics, maintenance, optimization and Windows integration.
- The WPF desktop application and self-contained CLI consume the same engine.
- There is one product architecture, not parallel script and native implementations.
- PowerShell, batch files and shell scripts are not product dependencies or execution backends.
- Do not add new PowerShell, batch or shell product code.
- WinGet is an explicit package-manager fallback only where a component declares it; official vendor sources take precedence.
- C++/Rust are reserved for a concrete native requirement that cannot be implemented cleanly in .NET.

## Engineering rules
- Prefer root-cause fixes over workarounds.
- Never suppress warnings/errors merely to make validation green.
- Keep installers idempotent and safe to re-run.
- Download to unique staging paths, validate, then atomically promote into the cache.
- Verify SHA-256 for cached artifacts and validate Authenticode for signed EXE/MSI artifacts.
- Keep older verified versions for rollback/reinstall.
- Do not store primary application state in `C:\ProgramData` or `%LOCALAPPDATA%`.
- Never persist secrets or registration tokens.
- Version checks must be component-specific.
- Failed staging must be cleaned without deleting valid cache entries.
- Destructive or irreversible actions require explicit confirmation.
- Optimization changes must have a rollback story where declared reversible.

## Git workflow
- GitHub is the source of truth.
- Work on feature branches and use PRs.
- **All GitHub Actions jobs must run on the user's self-hosted runner. No GitHub-hosted runner is permitted.**
- Workflows must use explicit self-hosted Windows labels matching the registered runner.
- The repository must not depend on Ubuntu/macOS hosted infrastructure.
- Never claim CI green without checking the exact commit.

## Execution-first
When the user authorizes implementation with `continue`, `vas-y`, `feu vert` or equivalent, execute repository changes before reporting them.

## Migration cleanup
The historical PowerShell/batch implementation is retired. Native .NET is the only product implementation.

Cleanup is complete only when the native engine and desktop are active, official-source/cache behavior and provisioning use C#, self-contained publish and fresh-Windows smoke validation are covered, all legacy `.ps1`, `.bat`, `.cmd` and product `.sh` paths are removed, and tests/workflows/docs no longer invoke script implementations.

Do not leave a PowerShell compatibility layer behind.
