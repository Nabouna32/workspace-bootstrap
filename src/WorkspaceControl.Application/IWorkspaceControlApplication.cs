using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public interface IWorkspaceControlApplication
{
    IReadOnlyList<WorkspaceManifest> GetWorkspaces();
    IReadOnlyList<ComponentManifest> GetApplicationCatalog();
    WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds);
    void SaveWorkspace(WorkspaceManifest workspace);
    Task<DesiredStateDiff> GetDesiredStateDiffAsync(string profileId, CancellationToken cancellationToken = default);
    Task<ProvisioningPlan> GetPlanAsync(string profileId, CancellationToken cancellationToken = default);
    Task<ProvisioningOperation> CreateProvisioningAsync(string profileId, CancellationToken cancellationToken = default);
    string ConfirmProvisioning(string operationId);
    Task RunProvisioningAsync(string operationId, CancellationToken cancellationToken = default);
    ProvisioningOperation? GetProvisioningStatus(string operationId);
    string ResumeProvisioning(string operationId);
    IReadOnlyList<ProvisioningOperation> GetProvisioningHistory();
    ProvisioningOperation? GetProvisioningRecovery();
    ProvisioningOperation? GetProvisioningDetail(string operationId);
    Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default);
    Task<SoftwareInventorySnapshot> GetSoftwareInventoryAsync(CancellationToken cancellationToken = default);
    BaselineSnapshot GetBaseline();
    IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan();
    IReadOnlyList<string> ApplySafeOptimization();
    IReadOnlyList<string> RollbackOptimization();
}
