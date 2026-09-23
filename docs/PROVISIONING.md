# Operations and provisioning

Provisioning is one operation family inside Workspace Control, not the product's architectural center.

## Lifecycle

```text
observed state → desired state → plan → awaiting-confirmation → queued → running
running → post-condition verification → completed
running → failed → recovery/resume
running → stale
```

A plan is read-only and is persisted as part of the `ProvisioningOperation`. Computing a plan never implies a mutation.

## Planning
Planning compares requested capability, observed state, desired state, providers, prerequisites, inventory diagnostics, privilege and risk. The resulting `ProvisioningPlan` contains the exact actions and observed preconditions that will be reviewed by the user.

The desktop Profiles surface exposes the plan before mutation. The user must explicitly confirm the persisted operation.

## Execution
After confirmation, Apply executes the persisted plan. It does not silently recompute or replace the plan.

Before any uncompleted mutation, the current observed state is compared with the corresponding planned precondition. If it differs, the operation becomes `stale`, is persisted in that terminal state and cannot be resumed. The user must generate and explicitly confirm a new plan.

Each completed mutation is followed by post-condition verification. After all planned steps, a final state verification confirms that the desired state is actually satisfied.

## Interactive installers
Resolve and verify the installer, launch it, wait for completion, rescan the machine and verify the expected state.

## Installer reuse
Installation is online-first. If a verified local artifact already matches the requested current version, it is reused without another download. There is no cache-only/offline provisioning mode.

## Recovery
Durable operations survive restart where technically possible. A failed operation may be resumed only when its persisted operation is explicitly resumable. Resume reuses the same confirmed plan and repeats precondition validation before any uncompleted mutation.

A stale operation is never resumed because doing so would execute against state different from the state the user confirmed.

## Cancellation
Cancellation is cooperative. If an external installer cannot safely stop, Workspace Control waits for a safe boundary.

## Safety
Destructive and irreversible actions require explicit confirmation. Failed downloads never invalidate a valid previously downloaded artifact.