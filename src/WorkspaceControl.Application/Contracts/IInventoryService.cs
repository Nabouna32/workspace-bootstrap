using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface IInventoryService
{
    Task<InventorySnapshot> ScanAsync(CancellationToken cancellationToken = default);
}
