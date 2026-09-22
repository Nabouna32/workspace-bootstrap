using System.Text.Json.Serialization;

namespace WorkspaceBootstrap.Desktop;

public sealed class DesktopCommandResponse<T>
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("command")]
    public string Command { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("exitCode")]
    public int ExitCode { get; init; }

    [JsonPropertyName("timestampUtc")]
    public DateTimeOffset TimestampUtc { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("messages")]
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public sealed class BaselineData
{
    public string? Windows { get; init; }
    public string? Build { get; init; }
    public string? Architecture { get; init; }
    public string? Cpu { get; init; }
    public int CpuCores { get; init; }
    public double MemoryGB { get; init; }
    public double UptimeHours { get; init; }
    public IReadOnlyList<VolumeData> Volumes { get; init; } = Array.Empty<VolumeData>();
    public IReadOnlyList<StartupEntry> StartupEntries { get; init; } = Array.Empty<StartupEntry>();
}

public sealed class VolumeData
{
    public string? DriveLetter { get; init; }
    public long Size { get; init; }
    public long SizeRemaining { get; init; }
    public string? HealthStatus { get; init; }
}

public sealed class StartupEntry
{
    public string? Name { get; init; }
}

public sealed class OptimizationPlanItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string StatusCode { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public bool Reversible { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class ProvisioningProfileOption
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class ProvisioningPlanData
{
    public ProvisioningProfileOption Profile { get; init; } = new();
    public IReadOnlyList<ProvisioningPlanItem> Items { get; init; } = Array.Empty<ProvisioningPlanItem>();
}

public sealed class ProvisioningPlanItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string StateCode { get; init; } = string.Empty;
    public string ActionCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class ProvisioningOperationStartData
{
    public string OperationId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class ProvisioningOperationHistoryItem
{
    public int SchemaVersion { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public int Completed { get; init; }
    public int Total { get; init; }
    public int Percent { get; init; }
    public string MessageKey { get; init; } = string.Empty;
    public bool CanResume { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ProvisioningOperationDetail {
    public ProvisioningOperationState Operation { get; init; } = new();
    public string RunId { get; init; } = string.Empty;
    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();
    public ProvisioningRunSummary? Summary { get; init; }
}

public sealed class ProvisioningRunSummary {
    public string RunId { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Selection { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public int ExitCode { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<ProvisioningRunStep> Steps { get; init; } = Array.Empty<ProvisioningRunStep>();
}

public sealed class ProvisioningRunStep {
    public string Id { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public string? Error { get; init; }
}

public sealed class ProvisioningOperationState
{
    public int SchemaVersion { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Phase { get; init; } = string.Empty;
    public string ProfileId { get; init; } = string.Empty;
    public string? CurrentComponentId { get; init; }
    public string? CurrentComponentName { get; init; }
    public int Completed { get; init; }
    public int Total { get; init; }
    public double Percent { get; init; }
    public string MessageKey { get; init; } = string.Empty;
    public string? Error { get; init; }
    public bool CanResume { get; init; }
    public int NextIndex { get; init; }
    public int? WorkerPid { get; init; }
    public string? RunId { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
