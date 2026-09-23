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

The non-application sections are declarative contracts at this stage. Their observation, diffing and execution capabilities are introduced incrementally through the same plan/confirm/apply/verify lifecycle. A profile containing unsupported sections is rejected before a mutation can be planned or applied; the engine never silently ignores desired state.

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

Profiles are versioned and portable. Exports never contain passwords, access tokens or other secrets.

## Automation

Interactive profiles require confirmation. Automated application is opt-in, explicit and auditable.