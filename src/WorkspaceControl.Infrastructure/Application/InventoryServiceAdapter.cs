using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Infrastructure.Application;

internal sealed class InventoryServiceAdapter : IInventoryService
{
    private readonly InventoryScanner _scanner;

    public InventoryServiceAdapter(InventoryScanner scanner) =>
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

    public Task<InventorySnapshot> ScanAsync(CancellationToken cancellationToken = default) =>
        _scanner.ScanAsync(cancellationToken);
}
