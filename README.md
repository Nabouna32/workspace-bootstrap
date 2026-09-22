# Workspace Bootstrap

**Workspace Bootstrap** is a native Windows 11 application for rebuilding, provisioning, maintaining and recovering a workstation after a clean Windows installation.

The product is built around **C#/.NET 10**. A native Windows desktop UI and a self-contained Windows CLI share the same Engine. The UI is a presentation layer; system changes, provisioning, cache management and recovery remain in the Engine.

## What it does

- Detects the current Windows state and installed applications.
- Lets you choose workstation profiles and individual components.
- Resolves current installers from official vendor sources.
- Keeps verified installers in the persistent local cache at `C:\DevCache`.
- Reuses verified cached installers instead of downloading them again.
- Keeps older verified versions for rollback and reinstallation.
- Verifies SHA-256 and Authenticode where applicable.
- Uses WinGet only as an explicit fallback declared by a component.
- Provides dry-run plans, progress, operation history, diagnostics, maintenance and guarded optimization.
- Produces a self-contained Windows x64 CLI suitable for a fresh Windows installation.

## Desktop application

The desktop application is designed as a real Windows 11 product rather than a thin launcher:

- Dashboard with workstation health and quick actions.
- Component/profile provisioning with plan preview before changes.
- Visible operation progress, recovery and history.
- Inventory and cleanup recommendations with explicit confirmation.
- Diagnostics and maintenance surfaces.
- Dark/light-ready design system with consistent cards, navigation, typography and state indicators.
- Engine-driven operations so the UI never becomes a second provisioning implementation.

The current presentation framework is WPF on .NET 10. WPF is only the UI layer; the application/domain/infrastructure logic remains ordinary C# in the shared Engine.

## Quick start

Build the product on Windows:

```text
dotnet restore tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj
dotnet build tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj --configuration Release
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Release
dotnet build src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release
```

Publish the self-contained CLI:

```text
dotnet publish src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release --runtime win-x64 --self-contained true
```

## Local installer cache

`C:\DevCache` is a persistent recovery/reinstallation asset, not a CI cache.

The cache uses component-specific version metadata and verified immutable artifacts. Downloads are staged under unique temporary names, verified, then promoted into the cache. Failed downloads never invalidate an existing verified artifact.

Before reinstalling Windows, export the cache to a non-system drive and restore it afterward.

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
- CI runs exclusively on GitHub-hosted runners.
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
