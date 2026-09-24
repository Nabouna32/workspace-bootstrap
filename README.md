# Workspace Control

**Workspace Control** is a permanent Windows 11 control center for managing a PC throughout its lifetime.

It brings software management, Windows configuration, diagnostics, maintenance, optimization, drivers, WSL and user-built desired-state Workspaces into one understandable application.

## Main capabilities

- Build a personal **My Workspace** by choosing what should be managed instead of being forced into a predefined setup.
- Compare the Workspace with the real PC and review risk/impact before applying changes.

- Install, update and uninstall applications.
- Discover applications even without a curated definition.
- Identify residuals conservatively.
- Reuse a verified local installer when it already matches the requested current version.
- Choose the appropriate installation source/provider, including interactive vendor installers.
- Inspect and configure supported Windows settings and policies.
- Recommend and apply documented optimizations.
- Diagnose Windows and application problems.
- Inspect and manage drivers with appropriate caution.
- Install and configure WSL.
- Create, edit, import, export and apply user-owned Workspaces (technical `ProfileManifest` documents).
- Inspect a read-only desired-state diff before provisioning.
- Use the same capabilities from the CLI.
- Extend the product through providers/plugins.

Workspace Control is not an antivirus, VPN, password manager or generic endpoint-security suite.

## User control

Normal interactive mode never silently changes the system. Provisioning follows an explicit cycle: observe state, calculate a plan, persist it, request user confirmation, apply exactly that confirmed plan, verify postconditions and verify the final state. If the machine changes between planning and apply, the operation is blocked rather than silently replanned. Every meaningful mutation explains what changes, why, scope, risk, reversibility and restart requirements. Irreversible actions are explicitly identified and require stronger confirmation.

## Portability

Workspace Control is distributed as a self-contained, unpackaged portable application. Application-owned Workspaces, configuration, cache, state and logs stay under the portable application directory; the product does not silently use AppData for its own state.

See [Portability](docs/PORTABILITY.md) and the [decision audit](docs/DECISIONS.md) for the storage and anti-drift contract.

## Technology

C#/.NET 10, WinUI 3 / Windows App SDK and Windows 11 x64. The CLI shares the same application/domain engine.

The GitHub repository is **Workspace Control**; the product identity and repository are intentionally aligned.

## Documentation

The product/UX contract is [Product experience](docs/PRODUCT-EXPERIENCE.md). It is authoritative for user-facing behavior and must not be replaced by implementation-specific summaries.

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