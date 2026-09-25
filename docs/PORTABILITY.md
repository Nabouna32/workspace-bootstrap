# Workspace Control — Portability

Workspace Control is distributed as a self-contained, unpackaged portable application.

## Application-owned data

The explicit portable application root owns:

- user Workspaces;
- preferences and application configuration;
- installer cache and metadata;
- staging data;
- operation/recovery state;
- optimization state;
- application-owned logs and diagnostics;
- future application-owned mutable data.

The default root is based on `AppContext.BaseDirectory`. Tests and controlled composition may inject an isolated root.

## Forbidden hidden persistence

Workspace Control must not silently use:

- `%LOCALAPPDATA%`;
- `%APPDATA%`;
- `ApplicationData.Current`;
- Registry-backed application configuration/state;
- another implicit per-user persistence location.

The Windows Registry, services, tasks and other Windows stores remain valid when they are the system being observed or managed.

Import/export destinations selected explicitly by the user are not hidden persistence.

## Deployment invariant

The application must not silently fall back to AppData when the portable root is unavailable or not writable.

Changing this storage model requires an explicit product/architecture decision and an impact audit.
