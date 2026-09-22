# Job execution

Workspace Bootstrap uses persistent C# job state for operations that may span application restarts or Windows reboots.

## Job lifecycle

planned -> queued -> running -> waiting-reboot -> completed
                         -> failed -> resumable

## Execution boundary

The job executor invokes the native .NET provisioning engine. A durable external operation identifier is persisted before long-running work begins.

## Resource safety

Jobs declare the capabilities and resources they require. The scheduler prevents conflicting operations from running concurrently and revalidates machine capabilities before executing a mutation.

## Recovery

After process restart, the scheduler reconciles unfinished jobs against the live machine state. A reboot-required job remains blocked until its prerequisite is observed.

Failed jobs are resumable only when the executor can prove that continuing is safe.
