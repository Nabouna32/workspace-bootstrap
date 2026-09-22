# Legacy desktop

This directory contains the **temporary WPF compatibility desktop** from the previous Workspace Bootstrap implementation.

It is being replaced by the native WinUI 3 application in `src/WorkspaceControl.Desktop`.

## Rules during migration

- Do not add new product capabilities to this WPF application.
- New UI work targets WinUI 3 / Windows App SDK.
- Shared product behavior belongs in the engine/domain layers, not in either UI.
- The WPF project remains buildable only to support incremental migration and rollback.
- Remove this project once the WinUI application reaches functional parity.

## Target desktop

```text
src/WorkspaceControl.Desktop
    └── WinUI 3 / Windows App SDK
             │
             └── shared Workspace Control engine/domain
```
