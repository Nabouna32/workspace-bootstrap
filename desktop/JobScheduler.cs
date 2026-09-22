namespace BounaDevEnvironment;

public sealed class JobScheduler
{
    private readonly JobStore _store;
    private readonly ICapabilitySnapshotProvider _capabilityProvider;

    public JobScheduler(JobStore store, ICapabilitySnapshotProvider capabilityProvider)
    {
        _store = store;
        _capabilityProvider = capabilityProvider;
    }

    public async Task<JobSchedulerSnapshot> RecoverAndReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        await _store.InitializeAsync(cancellationToken);
        await _store.RecoverInterruptedAsync(cancellationToken);

        var capabilities = await _capabilityProvider.GetCapabilitiesAsync(cancellationToken);
        var resourcePolicy = await _store.GetResourcePolicyAsync(cancellationToken);
        var jobs = (await _store.GetAllAsync(cancellationToken)).ToArray();

        foreach (var job in jobs)
        {
            if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled or JobStatus.Running or JobStatus.Paused or JobStatus.Recoverable)
                continue;

            var previousStatus = job.Status;
            var requirementsSatisfied = job.Definition.Requires.All(capabilities.Contains);
            var networkAllowed = !job.Definition.RequiresNetwork || resourcePolicy.AllowsNetworkStart;
            var recoveryRequired = previousStatus == JobStatus.Interrupted && !job.Definition.Idempotent;
            var nextStatus = !requirementsSatisfied
                ? JobStatus.Blocked
                : recoveryRequired
                    ? JobStatus.Recoverable
                    : !networkAllowed
                        ? JobStatus.WaitingForResource
                        : JobStatus.Ready;

            if (previousStatus != nextStatus)
            {
                job.Status = nextStatus;
                job.Error = null;
                job.MessageKey = !requirementsSatisfied
                    ? "job.waiting-for-capability"
                    : recoveryRequired
                        ? "job.recovery-required"
                        : !networkAllowed
                            ? "job.waiting-for-resource"
                            : "job.ready";
                await _store.UpdateAsync(job, cancellationToken);
            }
        }

        jobs = (await _store.GetAllAsync(cancellationToken)).ToArray();
        return new JobSchedulerSnapshot(jobs, capabilities, resourcePolicy);
    }

    public async Task<IReadOnlyList<JobRecord>> GetRunnableJobsAsync(
        IReadOnlySet<string> capabilities,
        IReadOnlySet<string>? occupiedResourceLocks = null,
        CancellationToken cancellationToken = default)
    {
        var resourcePolicy = await _store.GetResourcePolicyAsync(cancellationToken);
        var jobs = await _store.GetAllAsync(cancellationToken);
        var occupied = occupiedResourceLocks ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return jobs
            .Where(job => job.Status == JobStatus.Ready)
            .Where(job => job.Definition.Requires.All(capabilities.Contains))
            .Where(job => !job.Definition.RequiresNetwork || resourcePolicy.AllowsNetworkStart)
            .Where(job => job.Definition.ResourceLocks is null || !job.Definition.ResourceLocks.Any(lockId => occupied.Contains(lockId)))
            .OrderByDescending(job => job.Definition.Priority)
            .ThenBy(job => job.CreatedAt)
            .ToArray();
    }

    public static void MarkWaitingForReboot(JobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        job.Status = JobStatus.WaitingForReboot;
        job.WorkerId = null;
        job.MessageKey = "job.waiting-for-reboot";
        job.Error = null;
    }

    public static void MarkPaused(JobRecord job, string reasonKey = "job.paused")
    {
        ArgumentNullException.ThrowIfNull(job);
        job.Status = JobStatus.Paused;
        job.WorkerId = null;
        job.MessageKey = reasonKey;
        job.Error = null;
    }
}
