# Workspace Control — Profiles and Desired State

A profile describes the desired Windows workstation state. It is not merely a list of installers.

## Supported concepts
- Applications and version constraints.
- Windows settings and policies.
- Documented registry-backed settings.
- Drivers.
- WSL.
- Optimizations.
- Conditions based on machine facts.
- Future machine/group overrides.

## Local workflow
```text
Import/create profile
        ↓
Detect machine
        ↓
Evaluate conditions
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