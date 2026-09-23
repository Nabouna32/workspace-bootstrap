# Operations and provisioning

Provisioning is one operation family inside Workspace Control, not the product's architectural center.

## Lifecycle
```text
requested → planned → awaiting-confirmation → running
running → waiting-reboot / partially-completed / failed / cancelled
running → completed
```

## Planning
Planning compares requested capability, observed state, desired state, providers, prerequisites, cache availability, privilege and risk. Planning is read-only.

## Execution
Execution acquires resources, handles elevation, performs provider work, records progress, verifies postconditions, updates state and records diagnostics.

## Interactive installers
Resolve and verify the installer, launch it, wait for completion, rescan the machine and verify the expected state.

## Installer reuse
Installation is online-first. If a verified local artifact already matches the requested current version, it is reused without another download. There is no cache-only/offline provisioning mode.

## Recovery
Durable operations survive restart where technically possible. Recovery rescans the machine before continuing and avoids duplicate work.

## Cancellation
Cancellation is cooperative. If an external installer cannot safely stop, Workspace Control waits for a safe boundary.

## Safety
Destructive and irreversible actions require explicit confirmation. Failed downloads never invalidate a valid previously downloaded artifact.