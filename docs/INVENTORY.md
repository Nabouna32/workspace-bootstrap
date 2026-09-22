# Inventory

Inventory describes the Windows workstation as observed by the Engine.

## Providers

The initial providers cover:

- WinGet installed package metadata;
- MSI uninstall registry entries;
- AppX/package metadata where authoritative;
- Windows system capabilities;
- Workspace Bootstrap component state.

Providers must report evidence and ownership rather than guessing.

## Inventory item

Each item should expose:

- stable identifier;
- display name;
- publisher;
- installed version;
- source/evidence;
- ownership;
- installation scope;
- related component when known.

Cleanup recommendations are informational until the user explicitly confirms a destructive action.
