using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public sealed class WorkspaceControlApplication : IWorkspaceControlApplication
{
    private readonly IProvisioningService _provisioning;
    private readonly IInventoryService _inventory;
    private readonly IWindowsAdministrationService _windows;
    private readonly IOptimizationService _optimization;

    public WorkspaceControlApplication(
        IProvisioningService provisioning,
        IInventoryService inventory,
        IWindowsAdministrationService windows,
        IOptimizationService optimization)
    {
        _provisioning = provisioning ?? throw new ArgumentNullException(nameof(provisioning));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        _optimization = optimization ?? throw new ArgumentNullException(nameof(optimization));
    }

    public IReadOnlyList<ProfileManifest> GetProfiles() => _provisioning.GetProfiles();

    public Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
        _provisioning.GetPlanAsync(profileId, cancellationToken);

    public string StartProvisioning(string profileId, bool cacheOnly = false) =>
        _provisioning.Start(profileId, cacheOnly);

    public Task RunProvisioningAsync(
        string operationId,
        bool cacheOnly = false,
        CancellationToken cancellationToken = default) =>
        _provisioning.RunAsync(operationId, cacheOnly, cancellationToken);

    public ProvisioningOperation? GetProvisioningStatus(string operationId) =>
        _provisioning.Get(operationId);

    public string ResumeProvisioning(string operationId, bool cacheOnly = false) =>
        _provisioning.Resume(operationId, cacheOnly);

    public IReadOnlyList<ProvisioningOperation> GetProvisioningHistory() =>
        _provisioning.GetHistory();

    public ProvisioningOperation? GetProvisioningRecovery()
    {
        return _provisioning.GetHistory()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault(x => x.Status is "starting" or "running" or "failed");
    }

    public ProvisioningOperation? GetProvisioningDetail(string operationId) =>
        _provisioning.Get(operationId);

    public Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        _inventory.ScanAsync(cancellationToken);

    public BaselineSnapshot GetBaseline() => _windows.GetBaseline();

    public IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan() =>
        _optimization.GetSafePlan();

    public IReadOnlyList<string> ApplySafeOptimization() =>
        _optimization.ApplySafe();

    public IReadOnlyList<string> RollbackOptimization() =>
        _optimization.Rollback();
}
