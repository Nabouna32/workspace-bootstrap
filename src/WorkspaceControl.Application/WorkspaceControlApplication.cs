using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public sealed class WorkspaceControlApplication : IWorkspaceControlApplication
{
    private readonly IWorkspaceOperationService _workspaceOperations;
    private readonly IInventoryService _inventory;
    private readonly ISoftwareInventoryService _softwareInventory;
    private readonly IWindowsAdministrationService _windows;
    private readonly IOptimizationService _optimization;

    public WorkspaceControlApplication(
        IWorkspaceOperationService workspaceOperations,
        IInventoryService inventory,
        ISoftwareInventoryService softwareInventory,
        IWindowsAdministrationService windows,
        IOptimizationService optimization)
    {
        _workspaceOperations = workspaceOperations ?? throw new ArgumentNullException(nameof(workspaceOperations));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _softwareInventory = softwareInventory ?? throw new ArgumentNullException(nameof(softwareInventory));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _optimization = optimization ?? throw new ArgumentNullException(nameof(optimization));
    }

    public IReadOnlyList<WorkspaceManifest> GetWorkspaces() => _workspaceOperations.GetWorkspaces();
    public IReadOnlyList<ComponentManifest> GetApplicationCatalog() => _workspaceOperations.GetApplicationCatalog();
    public WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds) =>
        _workspaceOperations.CreateWorkspace(name, description, componentIds);
    public void SaveWorkspace(WorkspaceManifest workspace) => _workspaceOperations.SaveWorkspace(workspace);

    public Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _workspaceOperations.GetDesiredStateDiffAsync(workspaceId, cancellationToken);

    public Task<WorkspacePlan> CreateWorkspacePlanAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _workspaceOperations.GetPlanAsync(workspaceId, cancellationToken);

    public Task<WorkspaceOperation> CreateWorkspaceOperationAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _workspaceOperations.CreateAsync(workspaceId, cancellationToken);

    public string ConfirmWorkspaceOperation(string operationId) =>
        _workspaceOperations.Confirm(operationId);

    public Task RunWorkspaceOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        _workspaceOperations.RunAsync(operationId, cancellationToken);

    public WorkspaceOperation? GetWorkspaceOperation(string operationId) =>
        _workspaceOperations.Get(operationId);

    public string ResumeWorkspaceOperation(string operationId) =>
        _workspaceOperations.Resume(operationId);

    public IReadOnlyList<WorkspaceOperation> GetWorkspaceOperationHistory() =>
        _workspaceOperations.GetHistory();

    public WorkspaceOperation? GetRecoverableWorkspaceOperation()
    {
        var latest = _workspaceOperations.GetHistory()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        return latest?.Status is WorkspaceOperationStatuses.AwaitingConfirmation
            or WorkspaceOperationStatuses.Queued
            or WorkspaceOperationStatuses.Running
            or WorkspaceOperationStatuses.Failed
            ? latest
            : null;
    }

    public WorkspaceOperation? GetWorkspaceOperationDetail(string operationId) =>
        _workspaceOperations.Get(operationId);

    public Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        _inventory.ScanAsync(cancellationToken);

    public Task<SoftwareInventorySnapshot> GetSoftwareInventoryAsync(CancellationToken cancellationToken = default) =>
        _softwareInventory.ScanAsync(cancellationToken);

    public BaselineSnapshot GetBaseline() => _windows.GetBaseline();

    public IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan() =>
        _optimization.GetSafePlan();

    public IReadOnlyList<string> ApplySafeOptimization() =>
        _optimization.ApplySafe();

    public IReadOnlyList<string> RollbackOptimization() =>
        _optimization.Rollback();
}
