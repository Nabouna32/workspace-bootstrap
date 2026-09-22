# Provisioning

Provisioning is implemented in the native .NET engine.

## Desired state

A profile is a declarative list of component IDs. The engine resolves those IDs against the Windows component catalog and builds a plan before execution.

The plan is read-only. It identifies the profile, components and intended action.

## Installation flow

```
profile
  -> component manifest
  -> installed-state check
  -> official source resolution
  -> verified cache lookup
  -> unique staging download
  -> SHA-256 / Authenticode verification
  -> atomic cache promotion
  -> installer execution
  -> postcondition verification
  -> persistent operation state
```

WinGet is used only for components whose manifest explicitly declares `fallbackPackageManager: winget`.

## Cache-only mode

Cache-only provisioning never downloads. It succeeds only when a verified compatible installer is already present in the package cache.

## Recovery

Every provisioning operation has a durable operation identifier and persistent state. Recovery must never create duplicate installation work. A resume operation continues from the persisted checkpoint.

## Safety

- Installer execution is explicit and logged.
- Destructive operations require confirmation.
- Failed downloads are cleaned from staging only.
- Existing verified cache entries are never removed as a side effect of a failed update.
