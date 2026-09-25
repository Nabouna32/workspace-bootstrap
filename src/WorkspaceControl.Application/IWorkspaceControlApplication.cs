using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public interface IWorkspaceControlApplication
{
    IReadOnlyList<WorkspaceManifest> GetWorkspaces();
    IReadOnlyList<ComponentManifest> GetApplicationCatalog();
    WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds);
    void SaveWorkspace(WorkspaceManifest workspace);
    Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspacePlan> GetPlanAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceOperation> CreateProvisioningAsync(string workspaceId, CancellationToken cancellationToken = default);
    string ConfirmProvisioning(string operationId);
    Task RunProvisioningAsync(string operationId, CancellationToken cancellationToken = default);
    WorkspaceOperation? GetProvisioningStatus(string operationId);
    string ResumeProvisioning(string operationId);
    IReadOnlyList<WorkspaceOperation> GetProvisioningHistory();
    WorkspaceOperation? GetProvisioningRecovery();
    WorkspaceOperation? GetProvisioningDetail(string operationId);
    Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default);
    Task<SoftwareInventorySnapshot> GetSoftwareInventoryAsync(CancellationToken cancellationToken = default);
    BaselineSnapshot GetBaseline();
    IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan();
    IReadOnlyList<string> ApplySafeOptimization();
    IReadOnlyList<string> RollbackOptimization();
}
