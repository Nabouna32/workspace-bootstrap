namespace WorkspaceBootstrap.Desktop;

public sealed class JobRunner
{
    private readonly JobStore _store;
    private readonly JobScheduler _scheduler;
    private readonly ICapabilitySnapshotProvider _capabilityProvider;
    private readonly IReadOnlyDictionary<string, IJobExecutor> _executors;

    public JobRunner(
        JobStore store,
        JobScheduler scheduler,
        ICapabilitySnapshotProvider capabilityProvider,
        IEnumerable<KeyValuePair<string, IJobExecutor>> executors)
    {
        _store = store;
        _scheduler = scheduler;
        _capabilityProvider = capabilityProvider;
        _executors = executors.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var capabilitySnapshot = await _capabilityProvider.GetSnapshotAsync(cancellationToken);
        var jobs = await _store.GetAllAsync(cancellationToken);

        var occupiedLocks = jobs
            .Where(job => job.Status == JobStatus.Running)
            .SelectMany(job => job.Definition.ResourceLocks ?? Array.Empty<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var runnable = await _scheduler.GetRunnableJobsAsync(
            capabilitySnapshot.Capabilities,
            occupiedLocks,
            cancellationToken);

        foreach (var candidate in runnable)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
            if (!await _store.TryClaimAsync(candidate.Id, workerId, cancellationToken))
            {
                continue;
            }

            var claimed = (await _store.GetAllAsync(cancellationToken))
                .Single(job => job.Id.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase));

            await ExecuteClaimedAsync(claimed, cancellationToken);
            return true;
        }

        return false;
    }

    public async Task RunUntilIdleAsync(
        TimeSpan? idleDelay = null,
        CancellationToken cancellationToken = default)
    {
        var delay = idleDelay ?? TimeSpan.FromMilliseconds(500);

        while (!cancellationToken.IsCancellationRequested)
        {
            var executed = await RunOnceAsync(cancellationToken);
            if (!executed)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task ExecuteClaimedAsync(
        JobRecord job,
        CancellationToken cancellationToken)
    {
        if (!_executors.TryGetValue(job.Definition.Type, out var executor))
        {
            job.Status = JobStatus.Failed;
            job.Error = $"No executor is registered for job type '{job.Definition.Type}'.";
            job.MessageKey = "job.executor-missing";
            job.WorkerId = null;
            await _store.UpdateAsync(job, cancellationToken);
            return;
        }

        try
        {
            var result = await executor.ExecuteAsync(job, cancellationToken);

            if (result.Status == JobStatus.Completed)
            {
                var postcondition = await _capabilityProvider.GetSnapshotAsync(cancellationToken);
                var missingCapabilities = job.Definition.Provides
                    .Where(capability => !postcondition.Capabilities.Contains(capability))
                    .ToArray();

                if (missingCapabilities.Length > 0)
                {
                    job.Status = JobStatus.Failed;
                    job.Error = $"Job completed but did not establish required capabilities: {string.Join(", ", missingCapabilities)}.";
                    job.MessageKey = "job.postcondition-failed";
                }
                else
                {
                    job.Status = JobStatus.Completed;
                    job.Error = null;
                    job.MessageKey = result.MessageKey ?? "job.completed";
                }
            }
            else
            {
                job.Status = result.Status;
                job.Error = result.Error;
                job.MessageKey = result.MessageKey;
            }

            job.Progress = Math.Clamp(result.Progress, 0, 100);
            job.WorkerId = null;
            await _store.UpdateAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            job.Status = JobStatus.Interrupted;
            job.Error = "The job executor was interrupted.";
            job.MessageKey = "job.interrupted";
            job.WorkerId = null;
            await _store.UpdateAsync(job, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Interrupted;
            job.Error = "The job execution was cancelled.";
            job.MessageKey = "job.interrupted";
            job.WorkerId = null;
            await _store.UpdateAsync(job, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.MessageKey = "job.failed";
            job.WorkerId = null;
            await _store.UpdateAsync(job, cancellationToken);
        }
    }
}
