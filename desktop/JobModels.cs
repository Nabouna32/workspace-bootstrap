using System.Text.Json.Serialization;

namespace WorkspaceBootstrap.Desktop;

public enum JobStatus
{
    Queued,
    Blocked,
    Ready,
    Running,
    WaitingForResource,
    WaitingForReboot,
    Paused,
    Completed,
    Failed,
    Interrupted,
    Recoverable,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResourcePolicyMode
{
    Unlimited,
    Limited,
    Paused
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VersionSelectionMode
{
    Latest,
    Exact,
    Minimum,
    Range,
    Channel
}

public sealed record JobVersionPolicy(
    VersionSelectionMode Mode = VersionSelectionMode.Latest,
    string? Value = null,
    string? Channel = null)
{
    public bool IsPinned => Mode == VersionSelectionMode.Exact;
}

public sealed record JobResourcePolicy(
    ResourcePolicyMode NetworkMode = ResourcePolicyMode.Unlimited,
    long? MaxDownloadBytesPerSecond = null,
    int MaxConcurrentDownloads = 1)
{
    public bool AllowsNetworkStart => NetworkMode != ResourcePolicyMode.Paused;

    public bool HasBandwidthLimit =>
        NetworkMode == ResourcePolicyMode.Limited &&
        MaxDownloadBytesPerSecond is > 0;
}

public sealed record JobDefinition(
    string Id,
    string Type,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Provides,
    int Priority = 0,
    IReadOnlyList<string>? ResourceLocks = null,
    bool RequiresNetwork = false,
    bool Idempotent = true,
    JobVersionPolicy? VersionPolicy = null);

public sealed record JobExecutionContext(
    string ExecutionId,
    string? ExternalOperationId = null,
    string? StateJson = null);

public sealed class JobRecord
{
    public required string Id { get; init; }
    public required JobDefinition Definition { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int Attempts { get; set; }
    public double Progress { get; set; }
    public string? MessageKey { get; set; }
    public string? Error { get; set; }
    public string? WorkerId { get; set; }
    public JobExecutionContext? ExecutionContext { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record JobEnqueueRequest(JobDefinition Definition);

public sealed record JobSchedulerSnapshot(
    IReadOnlyList<JobRecord> Jobs,
    IReadOnlySet<string> AvailableCapabilities,
    JobResourcePolicy ResourcePolicy);

public sealed record CapabilitySnapshot(
    IReadOnlySet<string> Capabilities,
    DateTimeOffset ObservedAtUtc,
    string Source);

public interface ICapabilitySnapshotProvider
{
    Task<CapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    async Task<IReadOnlySet<string>> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        (await GetSnapshotAsync(cancellationToken)).Capabilities;
}


public interface IJobExecutionStateWriter
{
    Task PersistAsync(JobRecord job, CancellationToken cancellationToken = default);
}

public interface IJobExecutor
{
    Task<JobExecutionResult> ExecuteAsync(JobRecord job, CancellationToken cancellationToken);
}

public sealed record JobExecutionResult(
    JobStatus Status,
    double Progress = 100,
    string? MessageKey = null,
    string? Error = null);

public sealed class EmptyCapabilitySnapshotProvider : ICapabilitySnapshotProvider
{
    public Task<CapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CapabilitySnapshot(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            "empty"));
}
