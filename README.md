# Workspace Bootstrap

Workspace Bootstrap is a portable Windows 11 application for rebuilding, provisioning, maintaining and recovering a workstation after a clean Windows installation.

The product is built entirely with **C#/.NET 10**. The native Windows desktop UI and self-contained Windows CLI share the same Engine. The UI is a presentation layer; system changes, provisioning, cache management and recovery remain in the Engine.

## What it does

- Detects the current Windows state and installed applications.
- Lets you choose workstation profiles and individual components.
- Resolves current installers from official vendor sources.
- Stores verified installers in the application package cache.
- Reuses verified cached installers instead of downloading them again.
- Keeps older verified versions for rollback and reinstallation.
- Verifies SHA-256 and Authenticode where applicable.
- Uses WinGet only as an explicit fallback declared by a component.
- Provides dry-run plans, progress, operation history, diagnostics, maintenance and guarded optimization.
- Produces a self-contained Windows x64 application package.

## Portable application

The published package is self-contained:

```text
Workspace Bootstrap/
├── WorkspaceBootstrap.exe
├── WorkspaceBootstrap.Cli.exe
├── bootstrap/
├── cache/
├── state/
├── logs/
└── desktop-settings.json
```

The application does not require installation into a system directory and does not use a per-user application-data directory. Copying the package directory preserves the application and its local state.

## Desktop application

- Dashboard with workstation health and quick actions.
- Component/profile provisioning with plan preview before changes.
- Visible operation progress, recovery and history.
- Inventory and cleanup recommendations with explicit confirmation.
- Diagnostics and maintenance surfaces.
- System/light/dark theme selection.
- Consistent semantic status colors.
- Engine-driven operations so the UI never becomes a second provisioning implementation.

The current presentation framework is WPF on .NET 10. WPF is only the UI layer; application/domain/infrastructure logic remains ordinary C# in the shared Engine.

## Build

From the repository root on Windows:

```text
dotnet restore tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj
dotnet restore desktop/WorkspaceBootstrap.Desktop.csproj
dotnet restore src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj

dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Release
dotnet build src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release
dotnet test tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj --configuration Release
```

## Publish

The release workflow produces a portable Windows x64 package containing both the desktop application and CLI.

## Profiles

Profiles are declarative collections of components under `bootstrap/windows/profiles`.

## Design principles

- Root-cause fixes over workarounds.
- Reproducible and idempotent provisioning.
- Official vendor sources first.
- Verified local cache with immutable versioned entries.
- SHA-256 and Authenticode verification.
- WinGet only as an explicit fallback.
- Reversible changes keep their previous state.
- Irreversible changes require explicit confirmation.
- CI is validated on GitHub-hosted Windows runners.
- Credentials and secrets are never stored in ordinary configuration bundles.

## Documentation

- [Architecture](docs/ARCHITECTURE-V2.md)
- [Provisioning](docs/PROVISIONING.md)
- [Git workflow](docs/GIT-WORKFLOW.md)
- [Current status](docs/STATUS.md)
- [Roadmap](docs/ROADMAP.md)
- [Configuration](docs/CONFIGURATION.md)
- [Reinstallation](docs/REINSTALL.md)
- [CI and local builds](docs/CI-LOCAL-BUILD.md)

## License

MIT. See [LICENSE](LICENSE).
