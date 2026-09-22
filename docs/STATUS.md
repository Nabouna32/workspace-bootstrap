# Workspace Bootstrap — Current Status

Last updated: 2026-09-22

## Current direction

Workspace Bootstrap is now a **Windows-only native C#/.NET 10 product**.

The historical PowerShell, batch and WSL bootstrap implementations are retired. The product path is:

- native .NET engine;
- WPF desktop application;
- self-contained Windows x64 CLI;
- declarative Windows component/profile manifests;
- persistent local installer cache at `C:\DevCache`;
- application state under `C:\Dev\WorkspaceBootstrap`;
- self-hosted Windows GitHub Actions validation.

## Repository state

The canonical repository is `Nabouna32/workspace-bootstrap`.

The current refactor branch is `refactor/native-csharp-cutover`. It removes the legacy script implementation, removes out-of-scope WSL assets, normalizes the desktop namespace/project identity and replaces script-based validation with C# tests.

## Architecture

The .NET engine owns:

- component/profile loading;
- official-source and WinGet fallback resolution;
- installer staging and verification;
- cache metadata;
- provisioning operations;
- Windows baseline/diagnostics;
- optimization;
- maintenance and inventory services.

The desktop UI and CLI consume the same engine. They must not reimplement provisioning logic.

## CI

CI is intentionally **self-hosted only**. Workflows must run on:

`[self-hosted, windows, x64]`

No `ubuntu-latest`, `windows-latest` or `macos-latest` job is part of the product validation strategy.

Validation covers restore, Release builds, tests, self-contained CLI publish and repository policy checks.

## Remaining engineering work

- complete durable provisioning worker/recovery semantics;
- finish authoritative installed-state detection for each supported component;
- strengthen installer signature and digest handling;
- add integration tests for cache staging/promotion and WinGet fallback;
- add fresh-Windows smoke validation on an isolated Windows environment;
- finish desktop UX around profile selection, plan preview, progress, recovery and diagnostics;
- remove remaining obsolete documentation references to the retired implementation.

## Local validation

From a Windows checkout of this branch:

```text
dotnet restore tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Release
dotnet build src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release
dotnet test tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj --configuration Release
```

The exact branch must be tested before merging.
