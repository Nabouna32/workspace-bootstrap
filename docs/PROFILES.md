# Workspace Control — Profiles and Desired State

A profile is a versioned declarative description of the desired Windows workstation state. It is not merely a list of installers.

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

Applications reference the existing component catalog. Optional version constraints in a profile override the catalog policy for that profile only. The provisioning engine remains the single mutation engine; the desired-state model does not introduce a parallel installer path.

The desired-state diff is now available through the shared application contract and CLI. Application items are observed from the same inventory used by provisioning planning. Registry settings are observed with explicit hive/key/value/type evidence and can now be planned as reversible mutations. Registry Apply captures the existing value before writing, persists the snapshot in the operation journal, verifies the post-condition and rolls back registry mutations if the operation fails or is cancelled. Other non-application sections remain explicit contracts and appear as blocked diff items when populated.

## Local workflow

```text
Import/create profile
        ↓
Detect machine
        ↓
Evaluate conditions
        ↓
Observe desired-state domains
        ↓
Compare desired vs observed state
        ↓
Show diff
        ↓
User confirmation
        ↓
Apply operations
        ↓
Verify
```

## Heterogeneous machines

One profile may serve different machines. Conditions are evaluated against observed machine facts before a provisioning plan can be created. Current facts include Windows version/build, architecture, CPU, CPU cores, memory and uptime. Unknown facts fail closed: they produce a blocked diff rather than allowing mutation.

Conditions use explicit operators (`equals`, `not-equals`, `contains`, `greater-than`, `less-than`, `greater-or-equal`, `less-or-equal`). Numeric and version comparisons are handled as typed values where possible. Future per-machine overrides can build on this contract without changing the provisioning engine.

## Import/export

Profiles are versioned and portable. Export serializes a validated profile to a caller-selected path. Import validates the schema version, identifier contract and desired-state structure before writing it. Imports are written to a temporary file and atomically replace the destination only when serialization succeeds; an existing profile requires explicit overwrite. Profile content is data only and is never executed during import. Exports never contain passwords, access tokens or other secrets.

## Automation

Interactive profiles require confirmation. Automated application is opt-in, explicit and auditable.