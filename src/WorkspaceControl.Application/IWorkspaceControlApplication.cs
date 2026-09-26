using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public interface IWorkspaceControlApplication
{
    IReadOnlyList<WorkspaceManifest> GetWorkspaces();
    IReadOnlyList<ComponentManifest> GetApplicationCatalog();
    WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds);
    void SaveWorkspace(WorkspaceManifest workspace);
    Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspacePlan> CreateWorkspacePlanAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceOperation> CreateWorkspaceOperationAsync(string workspaceId, CancellationToken cancellationToken = default);
    string ConfirmWorkspaceOperation(string operationId);
    Task RunWorkspaceOperationAsync(string operationId, CancellationToken cancellationToken = default);
    WorkspaceOperation? GetWorkspaceOperation(string operationId);
    string ResumeWorkspaceOperation(string operationId);
    IReadOnlyList<WorkspaceOperation> GetWorkspaceOperationHistory();
    WorkspaceOperation? GetRecoverableWorkspaceOperation();
    WorkspaceOperation? GetWorkspaceOperationDetail(string operationId);
    Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default);
    Task<SoftwareInventorySnapshot> GetSoftwareInventoryAsync(CancellationToken cancellationToken = default);
    BaselineSnapshot GetBaseline();
    IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan();
    IReadOnlyList<string> ApplySafeOptimization();
    IReadOnlyList<string> RollbackOptimization();
}
