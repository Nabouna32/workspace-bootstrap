using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public sealed class WorkspaceControlApplication : IWorkspaceControlApplication
{
    private readonly IWorkspaceOperationService _provisioning;
    private readonly IInventoryService _inventory;
    private readonly ISoftwareInventoryService _softwareInventory;
    private readonly IWindowsAdministrationService _windows;
    private readonly IOptimizationService _optimization;

    public WorkspaceControlApplication(
        IWorkspaceOperationService provisioning,
        IInventoryService inventory,
        ISoftwareInventoryService softwareInventory,
        IWindowsAdministrationService windows,
        IOptimizationService optimization)
    {
        _provisioning = provisioning ?? throw new ArgumentNullException(nameof(provisioning));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _softwareInventory = softwareInventory ?? throw new ArgumentNullException(nameof(softwareInventory));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _optimization = optimization ?? throw new ArgumentNullException(nameof(optimization));
    }

    public IReadOnlyList<WorkspaceManifest> GetWorkspaces() => _provisioning.GetWorkspaces();
    public IReadOnlyList<ComponentManifest> GetApplicationCatalog() => _provisioning.GetApplicationCatalog();
    public WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds) =>
        _provisioning.CreateWorkspace(name, description, componentIds);
    public void SaveWorkspace(WorkspaceManifest workspace) => _provisioning.SaveWorkspace(workspace);

    public Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _provisioning.GetDesiredStateDiffAsync(workspaceId, cancellationToken);

    public Task<WorkspacePlan> CreateWorkspacePlanAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _provisioning.CreateWorkspacePlanAsync(workspaceId, cancellationToken);

    public Task<WorkspaceOperation> CreateWorkspaceOperationAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _provisioning.CreateAsync(workspaceId, cancellationToken);

    public string ConfirmWorkspaceOperation(string operationId) =>
        _provisioning.Confirm(operationId);

    public Task RunWorkspaceOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        _provisioning.RunAsync(operationId, cancellationToken);

    public WorkspaceOperation? GetWorkspaceOperation(string operationId) =>
        _provisioning.Get(operationId);

    public string ResumeWorkspaceOperation(string operationId) =>
        _provisioning.Resume(operationId);

    public IReadOnlyList<WorkspaceOperation> GetWorkspaceOperationHistory() =>
        _provisioning.GetHistory();

    public WorkspaceOperation? GetRecoverableWorkspaceOperation()
    {
        var latest = _provisioning.GetHistory()
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
        _provisioning.Get(operationId);

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
