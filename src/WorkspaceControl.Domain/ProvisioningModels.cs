using System.Text.Json.Serialization;

namespace WorkspaceControl.Domain;

public sealed record ComponentManifest(
    string Id,
    string Name,
    string[]? Profiles,
    string? PackageId,
    string? Source,
    string? InstallerType,
    string? Locale,
    string? Architecture,
    OfficialSource? OfficialSource,
    string[]? InstallArguments,
    string? FallbackPackageManager);

public sealed record OfficialSource(
    string Type,
    string? Repository = null,
    string? AssetRegex = null,
    string? VersionRegex = null);

public sealed record ProfileManifest(
    string Id,
    string Name,
    string Description,
    string[] Components);

public sealed record InstallerArtifact(
    string ComponentId,
    string Version,
    string FilePath,
    string Source,
    string Url,
    string Sha256);

public static class ProvisioningStateCodes
{
    public const string Missing = "MISSING";
    public const string Installed = "INSTALLED";
    public const string Outdated = "OUTDATED";
    public const string Unknown = "UNKNOWN";
}

public static class ProvisioningOperationStatuses
{
    public const string AwaitingConfirmation = "awaiting-confirmation";
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Stale = "stale";
}

public static class ProvisioningActionCodes
{
    public const string Install = "install";
    public const string Update = "update";
    public const string None = "none";
    public const string Blocked = "blocked";
}

public sealed record ProvisioningPlan(
    string ProfileId,
    string ProfileName,
    string InventoryScanId,
    IReadOnlyList<InventoryProviderDiagnostic> InventoryDiagnostics,
    IReadOnlyList<ProvisioningPlanItem> Items,
    DateTimeOffset CreatedAtUtc);

public sealed record ProvisioningPlanItem(
    string ComponentId,
    string ComponentName,
    string StateCode,
    string ActionCode,
    string? InstalledVersion,
    string Message);

public sealed record ProvisioningStep(
    string ComponentId,
    string Name,
    string Status,
    string? Error = null);

public sealed class ProvisioningOperation
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public string ProfileId { get; init; } = "";
    public string Status { get; set; } = ProvisioningOperationStatuses.AwaitingConfirmation;
    public ProvisioningPlan? Plan { get; set; }
    public int Completed { get; set; }
    public int Total { get; set; }
    public string? CurrentComponentName { get; set; }
    public string? Error { get; set; }
    public bool CanResume { get; set; }
    public List<ProvisioningStep> Steps { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class JsonDefaults
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
