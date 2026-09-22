using DevEnvironment.Execution;

namespace BounaDevEnvironment;

public sealed class ProvisioningJobExecutor : IJobExecutor
{
    private const string ProfilePrefix = "profile:";
    private readonly IProvisioningBackend _backend;
    private readonly IJobExecutionStateWriter _stateWriter;

    public ProvisioningJobExecutor(
        IProvisioningBackend backend,
        IJobExecutionStateWriter stateWriter)
    {
        _backend = backend;
        _stateWriter = stateWriter;
    }

    public async Task<JobExecutionResult> ExecuteAsync(
        JobRecord job,
        CancellationToken cancellationToken)
    {
        var profileId = GetProfileId(job);
        var context = job.ExecutionContext;

        if (context is null)
        {
            context = new JobExecutionContext(
                ExecutionId: Guid.NewGuid().ToString("N"));
            job.ExecutionContext = context;
        }

        ProvisioningOperationState state;

        if (string.IsNullOrWhiteSpace(context.ExternalOperationId))
        {
            var started = await _backend.StartAsync(profileId, cancellationToken);

            context = context with
            {
                ExternalOperationId = started.OperationId
            };
            job.ExecutionContext = context;

            // Persist the external operation ID before long-running polling so
            // a desktop crash cannot cause duplicate provisioning.
            await _stateWriter.PersistAsync(job, cancellationToken);
            state = await _backend.GetStatusAsync(started.OperationId, cancellationToken);
        }
        else
        {
            state = await _backend.GetStatusAsync(
                context.ExternalOperationId,
                cancellationToken);

            if (state.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)
                && state.CanResume)
            {
                var resumed = await _backend.ResumeAsync(
                    context.ExternalOperationId,
                    cancellationToken);

                // Resume is allowed to return a new operation identity. Persist
                // it before polling so a crash cannot reconnect to the obsolete
                // operation or start duplicate work on the next attempt.
                context = context with
                {
                    ExternalOperationId = resumed.OperationId
                };
                job.ExecutionContext = context;
                await _stateWriter.PersistAsync(job, cancellationToken);

                state = await _backend.GetStatusAsync(
                    resumed.OperationId,
                    cancellationToken);
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.Total > 0)
            {
                job.Progress = Math.Clamp(state.Percent, 0, 100);
            }

            job.MessageKey = string.IsNullOrWhiteSpace(state.MessageKey)
                ? "job.provisioning.running"
                : state.MessageKey;

            await _stateWriter.PersistAsync(job, cancellationToken);

            switch (state.Status.ToLowerInvariant())
            {
                case "completed":
                    return new JobExecutionResult(
                        JobStatus.Completed,
                        Math.Clamp(state.Percent, 0, 100),
                        "job.provisioning.completed");

                case "reboot-required":
                    return new JobExecutionResult(
                        JobStatus.WaitingForReboot,
                        Math.Clamp(state.Percent, 0, 100),
                        "job.provisioning.reboot-required");

                case "failed":
                    return state.CanResume
                        ? new JobExecutionResult(
                            JobStatus.Recoverable,
                            Math.Clamp(state.Percent, 0, 100),
                            "job.provisioning.recoverable",
                            state.Error)
                        : new JobExecutionResult(
                            JobStatus.Failed,
                            Math.Clamp(state.Percent, 0, 100),
                            "job.provisioning.failed",
                            state.Error);

                case "running":
                case "starting":
                    await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                    state = await _backend.GetStatusAsync(
                        context.ExternalOperationId
                            ?? throw new InvalidOperationException(
                                "L'opération de provisioning n'a pas d'identifiant persistant."),
                        cancellationToken);
                    continue;

                default:
                    return new JobExecutionResult(
                        JobStatus.Failed,
                        Math.Clamp(state.Percent, 0, 100),
                        "job.provisioning.unknown-status",
                        $"Unknown provisioning operation status '{state.Status}'.");
            }
        }
    }

    private static string GetProfileId(JobRecord job)
    {
        var profile = job.Definition.Requires
            .FirstOrDefault(requirement =>
                requirement.StartsWith(ProfilePrefix, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new InvalidOperationException(
                "Provisioning jobs must declare a profile requirement using 'profile:<profileId>'.");
        }

        var profileId = profile[ProfilePrefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new InvalidOperationException(
                "Provisioning job profile identifier cannot be empty.");
        }

        return profileId;
    }
}
