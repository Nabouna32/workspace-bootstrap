# Workspace Control — Portability and Application-Owned Storage Contract

This is a cross-cutting architecture contract. It is authoritative for where Workspace Control stores its own application data.

## Portable distribution

Workspace Control is distributed as a **self-contained, unpackaged Windows application** in a portable archive.

A portable copy must remain self-contained: moving the application directory to another writable location must move the application-owned configuration and state with it.

The application must not require a hidden per-user storage location to remain functional.

## Application-owned data

The following are application-owned data and therefore belong under the explicit portable application root:

- user-created Workspaces;
- application configuration and preferences;
- installer cache and verified installer metadata;
- staging data;
- operation state and recovery journals;
- optimization state;
- application-owned logs and diagnostics retained by the product;
- other mutable state introduced by future Workspace Control features.

The canonical layout is:

```text
<portable application root>/
├── WorkspaceControl.exe
├── bootstrap/
│   └── windows/
│       ├── components/
│       └── profiles/
├── workspaces/
├── cache/
│   ├── installers/
│   ├── metadata/
│   └── staging/
├── state/
└── logs/
```

Product code should derive this root from `AppContext.BaseDirectory` unless an explicit dependency-injection/test root is supplied.

## Forbidden hidden persistence

Workspace Control must not silently redirect application-owned data to:

- `%LOCALAPPDATA%`;
- `%APPDATA%`;
- `ApplicationData.Current` or equivalent Windows per-user application stores;
- the Registry for application-owned configuration/state;
- another implicit machine- or user-specific storage location.

An explicit user-selected import/export path is different: the user intentionally chose that destination and the product must not treat it as its hidden application store.

The Windows Registry, Windows services, scheduled tasks and other Windows stores may still be used when they are the **system target being observed or managed**. That does not make them valid stores for Workspace Control's own application state.

## Writable-location boundary

The supported portable artifact is expected to be extracted or copied to a location where the current user can write.

Workspace Control must not silently fall back to AppData when the chosen portable root is not writable. A future installer or managed deployment mode may introduce a different explicit storage contract, but that would be a documented product-direction change, not an invisible fallback.

## Testing and guardrails

Tests may inject an isolated root under a temporary directory for deterministic execution.

CI must reject new application-owned storage code that introduces hidden per-user persistence APIs. Any intentional exception must be a documented product decision and a deliberate architecture change.

Portable package smoke tests must exercise the published artifact, not only source-tree builds.

## Relationship with Workspaces

A Workspace is user-owned desired state. It is not a cloud-only object and it is not tied to a Windows user profile directory.

Copying or moving the portable Workspace Control directory must preserve the user's local Workspaces because they live under the application root.

Import/export remains available for deliberate sharing, backup or migration.
