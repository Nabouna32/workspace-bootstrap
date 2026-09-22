using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface ISoftwareInventoryService
{
    Task<SoftwareInventorySnapshot> ScanAsync(CancellationToken cancellationToken = default);
}
