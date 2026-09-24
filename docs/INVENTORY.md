# Workspace Control — Inventory

Inventory is the factual observation layer for the Windows machine. It is not a cleanup engine and it never changes the machine by itself.

## What can be inventoried

- installed applications and versions;
- installation source and provenance;
- Windows capabilities and configuration;
- drivers and hardware information;
- WSL distributions and configuration;
- services, scheduled tasks and other supported system resources;
- Workspace Control-managed desired state;
- update availability where a provider can establish it.

The catalog is not a hard limit. Unknown software can remain visible and may be handled through generic evidence-based providers.

## Evidence and ownership

Every observation should retain its provider, evidence, scope and confidence where applicable.

Ownership is explicit:

- System;
- Product-managed;
- Package-manager-managed;
- Manual;
- Unknown.

Unknown does not mean broken and must never be treated as permission to remove an item.

## Versioning

Inventory distinguishes installed version, available version and desired version. Version comparison is provider-aware and must not assume every application follows the same scheme.

## Cleanup

Residuals and orphan candidates are recommendations, not automatic deletion targets. Shared files, uncertain ownership and ambiguous evidence require additional review.

## Desired state

Inventory is compared with the user's Workspace (technical `ProfileManifest`) to produce a read-only diff and plan:

`Observed → Desired → Diff → Plan → Confirmation → Operation → Verify`

## Extensibility

Providers may add application, driver, Windows, WSL or hardware observations. Providers return structured evidence; product policy remains outside the provider.
