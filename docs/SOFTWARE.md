# Workspace Control — Software Management

Workspace Control should manage as much Windows software as can be handled safely, without being limited to a small personal catalog.

## Discovery
Inventory may combine Windows uninstall data, WinGet, AppX/MSIX metadata, provider data and future plugins.

Known applications have richer definitions for source selection, install, update, uninstall, cache and residual analysis. Unknown applications are still inventoried and managed only according to reliable evidence.

## Providers
Possible providers include official vendor installers, MSI/EXE, Microsoft Store/package providers, WinGet, portable packages and plugins. WinGet is a provider, not the architecture.

## Installation modes
- **Automatic:** validated provider contract.
- **Interactive:** launch vendor installer, wait, rescan and verify.
- **Advanced:** expose supported provider options.

## Updates
Show current version, available version, source, confidence and restart requirement. Users can update individually or in batches.

## Uninstall
Use the application's supported uninstaller/provider first. Residual analysis is conservative; uncertain or shared files are never removed automatically.

## Provenance
Managed items retain source/evidence so Workspace Control knows whether it manages, observes or merely discovered the software.

## Version policies
Supported semantics may include latest stable, exact, minimum, range and channel. Exact constraints are never silently relaxed.