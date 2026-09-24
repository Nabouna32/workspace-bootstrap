# Workspace Control — Profiles and Desired State

A **Workspace** is the user's desired Windows workstation state. A **ProfileManifest** is the versioned declarative technical representation of that state. The domain term `Profile` remains valid for schemas, APIs and imported/exported documents, while the primary user-facing term is **Workspace**.

A Workspace is not merely a list of installers.

## Schema versions

- **Schema 1** is the legacy application-only manifest containing `components`.
- **Schema 2** is the current desired-state manifest. It separates applications from Windows settings, policies, registry-backed settings, optimizations and machine conditions.

The runtime continues to read schema 1 profiles for compatibility, while built-in profiles use schema 2.

## Desired-state model

```text
Profile
  ├── applications
  ├── windowsSettings
  ├── policies
  ├── registrySettings
  ├── optimizations
  └── conditions
```

Applications reference the existing component catalog. Each application may declare `state: present` (the default) or `state: absent`; optional version constraints override the catalog policy for that profile only. An absent application is planned as a persisted uninstall operation when reliable inventory and rollback evidence are available. Removal captures the installed version before mutation, verifies absence afterward and restores the exact version through the same provisioning engine if a later operation fails. The provisioning engine remains the single mutation engine; the desired-state model does not introduce a parallel installer path.

The desired-state diff is now available through the shared application contract and CLI. Application items are observed from the same inventory used by provisioning planning. Registry settings are observed with explicit hive/key/value/type evidence and can now be planned as reversible mutations. Registry Apply captures the existing value before writing, persists the snapshot in the operation journal, verifies the post-condition and rolls back registry mutations if the operation fails or is cancelled. Other non-application sections remain explicit contracts and appear as blocked diff items when populated.

## User-facing workflow

```text
Open My Workspace
        ↓
Create or edit desired state
        ↓
Detect current machine
        ↓
Evaluate conditions
        ↓
Observe desired-state domains
        ↓
Compare desired vs observed state
        ↓
Review changes, risk and impact
        ↓
Explicit confirmation
        ↓
Apply the persisted plan
        ↓
Verify
```

A predefined profile is never required. Product templates, when supplied, are optional starting points that become user-owned Workspaces after editing.

## Heterogeneous machines

One profile may serve different machines. Conditions are evaluated against observed machine facts before a provisioning plan can be created. Current facts include Windows version/build, architecture, CPU, CPU cores, memory and uptime. Unknown facts fail closed: they produce a blocked diff rather than allowing mutation.

Conditions use explicit operators (`equals`, `not-equals`, `contains`, `greater-than`, `less-than`, `greater-or-equal`, `less-or-equal`). Numeric and version comparisons are handled as typed values where possible. Future per-machine overrides can build on this contract without changing the provisioning engine.

## Import/export

Profiles are versioned and portable. Export serializes a validated profile to a caller-selected path. Import validates the schema version, identifier contract and desired-state structure before writing it. Imports are written to a temporary file and atomically replace the destination only when serialization succeeds; an existing profile requires explicit overwrite. Profile content is data only and is never executed during import. Exports never contain passwords, access tokens or other secrets.

## Automation

Interactive Workspaces require confirmation. Automated application is opt-in, explicit and auditable.