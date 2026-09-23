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

The desired-state diff is now available through the shared application contract and CLI. Application items are observed from the same inventory used by provisioning planning. Non-application sections are explicit contracts but are not executable yet: they appear as blocked diff items, and provisioning refuses to create a mutation plan until those sections have an observer/planner.

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

One profile may serve different machines. Conditions and future per-machine overrides allow a common baseline without assuming identical hardware or software.

## Import/export

Profiles are versioned and portable. The configuration layer validates schema version and profile identifiers before import, writes imports through a temporary file and atomically replaces the destination only after serialization succeeds. Existing profiles are never overwritten unless the caller explicitly opts in. Exports never contain passwords, access tokens or other secrets.

## Automation

Interactive profiles require confirmation. Automated application is opt-in, explicit and auditable.