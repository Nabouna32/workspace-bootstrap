using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface ISoftwareInventorySource
{
    string Id { get; }
    Task<SoftwareInventorySourceResult> ScanAsync(CancellationToken cancellationToken = default);
}

public sealed record SoftwareInventorySourceResult(
    IReadOnlyList<SoftwareItem> Items,
    IReadOnlyList<string> Diagnostics);
