# Workspace Control

**Workspace Control** is a permanent Windows 11 control center for managing a PC throughout its lifetime.

It brings software management, Windows configuration, diagnostics, maintenance, optimization, drivers, WSL, profiles and offline cache into one understandable application.

## Main capabilities

- Install, update and uninstall applications.
- Discover applications even without a curated definition.
- Identify residuals conservatively.
- Maintain a verified offline software cache.
- Choose the appropriate installation source/provider, including interactive vendor installers.
- Inspect and configure supported Windows settings and policies.
- Recommend and apply documented optimizations.
- Diagnose Windows and application problems.
- Inspect and manage drivers with appropriate caution.
- Install and configure WSL.
- Create, import, export and apply desired-state profiles.
- Use the same capabilities from the CLI.
- Extend the product through providers/plugins.

Workspace Control is not an antivirus, VPN, password manager or generic endpoint-security suite.

## User control

Normal interactive mode never silently changes the system. Every meaningful mutation explains what changes, why, scope, risk, reversibility and restart requirements. Irreversible actions are explicitly identified and require stronger confirmation.

## Technology

C#/.NET 10, WinUI 3 / Windows App SDK and Windows 11 x64. The CLI shares the same application/domain engine.

The GitHub repository is **Workspace Control**; the product identity and repository are intentionally aligned.

## Documentation

The complete documentation index is in [docs/README.md](docs/README.md).

Start with:

- [Product vision](docs/PRODUCT-VISION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [UX design](docs/UX-DESIGN.md)
- [Profiles](docs/PROFILES.md)
- [Software management](docs/SOFTWARE.md)
- [Security](docs/SECURITY.md)
- [Roadmap](docs/ROADMAP.md)

## Development

GitHub is the source of truth. CI uses GitHub-hosted Windows runners. Self-hosted runners are not part of the project.

## License

MIT.