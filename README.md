# Dev Environment

Reproducible Windows development-environment bootstrap and maintenance tooling.

The project provides a declarative component/profile model, interactive provisioning, diagnostics, maintenance, optimization, a persistent local installer cache, and a native desktop management application.

## Quick start

On a fresh Windows 11 installation, run the bootstrap launcher from an elevated PowerShell session:

    .\bootstrap\windows\dev-env.ps1

Preview the provisioning plan without installing or changing anything:

    .\bootstrap\windows\dev-env.ps1 -PlanOnly

Export the local installer cache before reinstalling Windows:

    .\bootstrap\windows\dev-env.ps1 -ExportCacheTo E:\DevCache

The installer cache is a local recovery/reinstallation asset. It is not a GitHub Actions cache.

## Profiles

- **Base** — general Windows workstation foundation.
- **Development** — Windows/WSL development toolchain.
- **Development Extended** — additional IDEs, languages and tooling.
- **Gaming** — gaming-related applications and configuration.

## Design principles

- Root-cause fixes over workarounds.
- Reproducible, idempotent provisioning.
- Official vendor sources preferred for installer resolution and download.
- Verified local installer cache with immutable versioned entries.
- SHA-256 verification when an upstream digest is published.
- Authenticode validation for cached EXE/MSI installers.
- WinGet used only as an explicit fallback when no automated official source is declared.
- Reversible changes keep their previous state.
- Irreversible changes require explicit confirmation.
- GitHub-hosted standard runners provide the authoritative CI validation.
- Credentials and secrets are never stored in ordinary configuration bundles.

## Documentation

- [Architecture](docs/ARCHITECTURE-V2.md)
- [Provisioning](docs/PROVISIONING.md)
- [Git workflow](docs/GIT-WORKFLOW.md)
- [Current status](docs/STATUS.md)
- [Roadmap](docs/ROADMAP.md)
- [Configuration](docs/CONFIGURATION.md)
- [WSL development](docs/WSL-DEVELOPMENT.md)
- [CI and local builds](docs/CI-LOCAL-BUILD.md)

## License

MIT. See [LICENSE](LICENSE).
