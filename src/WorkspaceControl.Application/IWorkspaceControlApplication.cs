using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public interface IWorkspaceControlApplication
{
    IReadOnlyList<ProfileManifest> GetProfiles();
    Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default);
    string StartProvisioning(string profileId, bool cacheOnly = false);
    Task RunProvisioningAsync(string operationId, bool cacheOnly = false, CancellationToken cancellationToken = default);
    ProvisioningOperation? GetProvisioningStatus(string operationId);
    string ResumeProvisioning(string operationId, bool cacheOnly = false);
    IReadOnlyList<ProvisioningOperation> GetProvisioningHistory();
    ProvisioningOperation? GetProvisioningRecovery();
    ProvisioningOperation? GetProvisioningDetail(string operationId);
    Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default);
    BaselineSnapshot GetBaseline();
    IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan();
    IReadOnlyList<string> ApplySafeOptimization();
    IReadOnlyList<string> RollbackOptimization();
}
