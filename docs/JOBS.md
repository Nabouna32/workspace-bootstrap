# Job Orchestration Architecture

## Purpose

The desktop application uses a persistent job scheduler for long-running and multi-step work. A job is an independent unit of work that can remain queued while its prerequisites are unavailable and become runnable later without depending on the historical result of another job.

The queue is part of the product architecture, not a UI convenience.

## Core model

Jobs declare:

- a stable job ID;
- a job type and version;
- required capabilities;
- capabilities provided after successful verification;
- priority;
- execution/retry policy;
- persistent lifecycle state.

A job does **not** normally depend on another job ID. It depends on capabilities.

Example:

```text
wsl.install
  provides -> wsl.available

ubuntu.install
  requires -> wsl.available
```

If WSL installation requires a reboot, `wsl.available` remains unsatisfied until the scheduler verifies WSL after the reboot. Ubuntu is therefore `Blocked`, not `Failed`.

## Persistent queue

The queue is stored in SQLite under the user's local application data. The database is independent from the WPF process and must survive:

- application close;
- application crash;
- Windows restart;
- reboot-required provisioning steps;
- temporary worker interruption.

SQLite is used directly rather than introducing an ORM. The job scheduler owns the persistence contract; each state write is atomic, and multi-job transitions will use explicit SQLite transactions as the scheduler evolves.

## Recovery

At application startup the scheduler:

1. loads persisted jobs;
2. identifies jobs that were running when the previous process disappeared;
3. marks them `interrupted` according to the recovery policy;
4. re-detects capabilities from the current machine state;
5. recomputes which jobs are blocked, ready, waiting for reboot or recoverable;
6. resumes execution when the job's prerequisites are satisfied.

The scheduler must never infer success merely because a previous process reached a particular point. Verification of the current machine is authoritative.

## Reboot

A reboot is a scheduler state, not an application error.

A job may return `waiting-reboot` and remain persisted. Jobs that do not require the same resource may continue when safe. After Windows starts again, the scheduler verifies the relevant capabilities and unblocks dependent jobs automatically.

## Independence

Jobs are independent by default:

- a failed job does not automatically fail unrelated jobs;
- a blocked job does not prevent independent ready jobs from running;
- a dependent job waits for its required capabilities;
- a capability may be provided by a previous run, manual installation, or another job.

This makes the queue declarative and idempotent.

## Resource safety

Capability dependencies are not enough for concurrent execution. Jobs may also declare resource locks such as:

- `windows.configuration`;
- `wsl.configuration`;
- `network.configuration`;
- `read-only.system`.

The scheduler must not execute conflicting jobs concurrently.

## Execution boundary and migration

C#/.NET is the strategic execution/orchestration core. It owns job orchestration, persistence, lifecycle, scheduling, recovery and the future execution abstractions.

PowerShell remains the compatibility backend for capabilities that have not yet migrated. New execution work should be introduced behind an explicit C# abstraction and should reuse the existing PowerShell backend where necessary rather than duplicating installer logic. Migration is incremental: preserve operation IDs, verification, reboot handling and recovery semantics while replacing one execution responsibility at a time.

PowerShell should expose stable machine-readable contracts and use English for engine/operator-facing messages. The WPF application owns localization of user-facing presentation (FR/EN/system language).

## Public-project readiness

Job IDs, capability IDs, state tokens and contracts are language-neutral and stable. Human-readable labels are presentation concerns and must not be persisted as the semantic identity of a job or capability.

Any new job type should document:

1. required capabilities;
2. provided capabilities;
3. resource locks;
4. detection/verification rules;
5. reboot behavior;
6. retry/recovery semantics;
7. whether the operation is idempotent.

## Initial implementation boundary

The first implementation introduces the persistent SQLite job store and capability-aware scheduler primitives without replacing the existing provisioning worker in one large migration.

The provisioning operation will be migrated to this scheduler incrementally, with existing resume/recovery behavior preserved until the new path is validated.
## Resource policies and download control

The scheduler also owns resource policies for operations that consume shared machine resources. Resource locks prevent incompatible work from running concurrently; resource policies govern how available resources may be consumed.

The first resource-policy target is network activity:

- Unlimited — no scheduler-imposed bandwidth cap;
- Limited — apply a maximum rate where the downloader is under our control;
- Paused — prevent eligible network jobs from starting or pause them at a safe execution boundary;
- optional future budgets/windows may constrain total data or allowed download times.

Bandwidth control is capability-dependent. The scheduler must **not** claim exact throttling for third-party tools whose network behavior it cannot control. For example, WinGet or an external installer may be delayed before launch, but the scheduler cannot guarantee its internal transfer rate unless the download path is explicitly controlled by our own downloader or a supported mechanism.

Pausing must be distinct from cancellation. A pause request must never blindly terminate an installer or leave Windows/WSL in a partially modified state. If an external operation cannot be safely paused, the scheduler finishes the current atomic step and transitions to a paused/waiting state at the next safe boundary.

Resource policy is scheduler state, not failure. A job waiting for a network policy, budget, time window or resource lock remains recoverable and can resume automatically when the policy permits it.

## Job execution semantics

The scheduler distinguishes:

- `Blocked` — a required capability is not currently satisfied;
- `WaitingForResource` — required resources/policies are currently unavailable;
- `WaitingForReboot` — the operation requires a Windows reboot before verification can continue;
- `Paused` — execution was intentionally suspended and can resume;
- `Interrupted` — the process disappeared unexpectedly and recovery must verify the machine before retrying;
- `Recoverable` — the job can safely resume or retry after verification;
- `Failed` — execution completed unsuccessfully and requires an explicit retry/recovery decision;
- `Cancelled` — the user explicitly stopped the job.

A job may declare execution properties such as:

- idempotent/non-idempotent;
- resumable/non-resumable;
- retry policy;
- reboot behavior;
- resource locks;
- network requirement and download-control capability.

The scheduler must not automatically retry a non-idempotent operation unless its executor explicitly proves that the retry is safe.

## Persistent execution context

A job snapshot is not sufficient for long-running external work. When an executor starts an operation outside the desktop process, it must persist a runtime execution context separately from the immutable job definition.

The local queue persists:

- a unique executionId for the current execution attempt;
- an optional externalOperationId used to reconnect to an existing engine operation;
- optional executor-owned state JSON for small, versioned runtime metadata.

This context is deliberately distinct from JobDefinition: desired work is stable, while execution state changes as the operation progresses.

For recoverable external operations, the executor should first inspect persisted context after restart and reconnect to the existing operation when the underlying engine supports it. It must not start a duplicate operation merely because the desktop process restarted.

Executor-owned state must remain bounded and schema-versioned. Large logs belong in the structured log/run storage, not in the SQLite job row.

## User-facing explainability

Every non-running job should have a machine-readable reason for its current state. The WPF layer will localize that reason without changing its stable semantic identifier.

Examples include:

```text
WaitingForCapability: wsl.available
WaitingForResource: network.paused
WaitingForResource: network.bandwidth-limit
WaitingForReboot: windows.reboot-required
BlockedByLock: wsl.configuration
```

This is a product requirement: the application should explain **why** a job is waiting rather than displaying a generic failure.

## Future resource model

The current `resourceLocks` foundation will evolve toward a scheduler resource model covering, where useful:

- network bandwidth and download budgets;
- concurrent download limits;
- installer concurrency;
- Windows configuration;
- WSL configuration;
- disk pressure;
- CPU/memory pressure when measurable and actionable;
- scheduled download windows.

These policies are additive to capability dependencies. A job can be capability-ready while still waiting for a resource policy.


## Configuration-driven job generation

A future configuration importer generates persistent jobs from desired state. It validates the bundle, compares it with current capabilities and component checks, creates only the work required to converge, records an import operation ID, and lets independent jobs continue while another waits or fails. The current machine state remains authoritative.


## Version selection

Version is part of desired state when a component supports versioned installation. Jobs distinguish the selection policy from the detected installed version:

- `latest`: resolve the currently supported version;
- `exact`: require the requested version;
- `minimum`: accept a compatible version at or above the requested version;
- `range`: accept only versions inside the declared range;
- `channel`: resolve from a supported release channel.

An exact version request must never silently fall back to another version. If the requested artifact is unavailable, the job is surfaced as a version conflict and remains visible to the user. The scheduler must not mutate the requested version behind the user's back.

Version resolution should happen before a destructive install step whenever possible. The resolved version, source and selection policy should be persisted with the job/run record so a restart can explain exactly what was requested and what was installed.


## Removal jobs

Removal is a first-class job action, not an ad-hoc UI command. A future job type such as `package.remove` uses the same persistence, capability resolution, resource locking, recovery and logging rules as installation jobs.

Removal jobs must carry enough metadata to make the operation auditable:

- package/component identity;
- detected installed version;
- installation source and package manager when known;
- user/system scope;
- ownership classification;
- capabilities expected to disappear;
- reinstall/restore path when known;
- reboot behavior;
- idempotency and retry rules.

A successful removal must **not** automatically publish a capability as absent solely from the executor's exit code. The postcondition must be verified against the machine before dependent desired-state decisions are recalculated.

Debloat is represented as an explicit policy that generates concrete removal jobs. The policy itself is not allowed to bypass the normal job lifecycle. This keeps aggressive debloat observable, resumable and auditable and prevents a bulk cleanup operation from becoming an untracked destructive script.

If a component is simultaneously declared as desired-installed and selected for removal, the scheduler must surface the conflict before executing either destructive action. It must not silently oscillate between install and uninstall.


## Inventory-driven cleanup

Inventory is an observation layer feeding desired-state reconciliation; it is not itself an execution engine. A scan may discover multiple versions of the same toolchain family and may create cleanup recommendations, but only an approved plan creates removal jobs.

A cleanup recommendation should include:

- canonical component identity;
- installed version(s);
- detected newer version, when applicable;
- provider/source and ownership;
- scope;
- capabilities provided;
- usage/dependency evidence;
- desired-state conflicts;
- supported uninstall mechanism;
- reboot/recovery implications;
- recommendation reason and evidence confidence.

The scheduler must not interpret `older` as `safe-to-remove`. A JDK 17 installation can be older than JDK 21 and still be required by a project. Version-family cleanup therefore requires usage and desired-state checks before a `package.remove` job is created.

The removal job must revalidate the inventory item immediately before execution, because the scan may be stale. After execution, the executor verifies the removal postcondition with the appropriate inventory provider before publishing capability changes or recalculating dependents.

## Executor registry and durable execution state

The long-term job runner uses an explicit executor registry keyed by stable job type. Registration must be validated at startup and duplicate/unknown types must fail clearly rather than silently selecting a fallback executor.

Executors may perform long-running external work. Any external operation identifier required to reconnect after process restart must be persisted before polling or other non-idempotent waiting begins. Executor state is bounded and schema-versioned; detailed logs belong in run/log storage, not the job row.

Progress and state persistence are part of execution semantics, not presentation-only state. A future executor contract should support durable progress updates and explicit recovery/cancellation outcomes without killing installers blindly.

