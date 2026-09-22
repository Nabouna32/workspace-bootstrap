# Workspace Bootstrap

**Workspace Bootstrap** is a native Windows 11 application for rebuilding, provisioning, maintaining and recovering a workstation after a clean Windows installation.

The product is implemented in **C#/.NET 10** with a WPF desktop application and a self-contained Windows CLI sharing the same engine. There is no PowerShell or batch execution layer.

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
- Produces a self-contained Windows x64 application suitable for a fresh Windows installation.

## Quick start

Build the product on Windows:

```text
dotnet restore src/WorkspaceBootstrap.Engine/WorkspaceBootstrap.Engine.csproj
dotnet build src/WorkspaceBootstrap.Engine/WorkspaceBootstrap.Engine.csproj --configuration Release
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Release
dotnet build src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release
```

Publish the self-contained CLI:

```text
dotnet publish src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release --runtime win-x64 --self-contained true
```

The desktop application and CLI use the same native engine; neither requires PowerShell 7.

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
- CI runs exclusively on the project's self-hosted Windows runner.
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
