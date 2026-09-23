namespace WorkspaceControl.Domain;

public enum SoftwareOwnership
{
    System,
    ProductManaged,
    PackageManagerManaged,
    Manual,
    Unknown
}

public enum SoftwareScope
{
    User,
    System,
    Unknown
}

public enum SoftwareSignal
{
    Installed,
    Active,
    Required,
    OlderVersion,
    UnusedDetected,
    OrphanCandidate,
    Unknown
}

public sealed record SoftwareEvidence(
    string Source,
    string Signal,
    string? Detail = null,
    DateTimeOffset? ObservedAt = null,
    bool IsStrong = false);

public sealed record SoftwareItem(
    string Id,
    string Name,
    string? Version,
    string? Publisher,
    string? InstallLocation,
    SoftwareOwnership Ownership,
    SoftwareScope Scope,
    IReadOnlyList<SoftwareSignal> Signals,
    IReadOnlyList<SoftwareEvidence> Evidence,
    string? AvailableVersion = null);

public sealed record SoftwareInventorySnapshot(
    string ScanId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<SoftwareItem> Items,
    IReadOnlyList<string> Diagnostics);
