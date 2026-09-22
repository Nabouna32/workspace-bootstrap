using System.Text.Json.Serialization;

namespace WorkspaceBootstrap;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InventoryOwnership
{
    System,
    ProductManaged,
    PackageManagerManaged,
    Manual,
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InventoryScope
{
    User,
    System,
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InventorySignal
{
    Installed,
    Active,
    Required,
    OlderVersion,
    UnusedDetected,
    OrphanCandidate,
    Unknown
}

public sealed record InventoryEvidence(
    string Kind,
    string Description,
    bool IsStrong,
    string? Source = null);

public sealed record InventoryObservation(
    string Id,
    string DisplayName,
    string? Version,
    string? FamilyId,
    string Provider,
    string? ProviderId,
    string? Source,
    InventoryScope Scope,
    string? InstallLocation,
    string? RemovalCommand,
    string? Publisher,
    InventoryOwnership Ownership,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<InventoryEvidence> Evidence,
    DateTimeOffset DetectedAtUtc);

public sealed class InventoryItem
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Version { get; init; }
    public string? FamilyId { get; init; }
    public InventoryScope Scope { get; init; }
    public string? InstallLocation { get; init; }
    public string? RemovalCommand { get; init; }
    public string? Publisher { get; init; }
    public InventoryOwnership Ownership { get; init; }
    public required IReadOnlyList<string> Capabilities { get; init; }
    public required IReadOnlyList<InventoryObservation> Observations { get; init; }
    public required IReadOnlyList<InventoryEvidence> Evidence { get; init; }
    public required ISet<InventorySignal> Signals { get; init; }
    public DateTimeOffset DetectedAtUtc { get; init; }
}

public sealed record InventoryProviderDiagnostic(
    string ProviderId,
    bool Success,
    string Message,
    string? Detail = null);

public sealed record InventorySnapshot(
    string ScanId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<InventoryItem> Items,
    IReadOnlyList<InventoryProviderDiagnostic> ProviderDiagnostics);

public sealed record InventoryCleanupRecommendation(
    string InventoryItemId,
    string DisplayName,
    string? InstalledVersion,
    string? NewerVersion,
    string? Publisher,
    string ReasonKey,
    IReadOnlyList<string> Evidence,
    bool RequiresExplicitApproval);

public interface IInventoryProvider
{
    string Id { get; }

    Task<InventoryProviderResult> ScanAsync(
        CancellationToken cancellationToken = default);
}


public sealed record InventoryProviderResult(
    IReadOnlyList<InventoryObservation> Observations,
    InventoryProviderDiagnostic Diagnostic);
