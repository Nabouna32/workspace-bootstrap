using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application;

public sealed class WorkspaceControlApplication : IWorkspaceControlApplication
{
    private readonly IProvisioningService _provisioning;
    private readonly IInventoryService _inventory;
    private readonly ISoftwareInventoryService _softwareInventory;
    private readonly IWindowsAdministrationService _windows;
    private readonly IOptimizationService _optimization;

    public WorkspaceControlApplication(
        IProvisioningService provisioning,
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

    public IReadOnlyList<ProfileManifest> GetProfiles() => _provisioning.GetProfiles();

    public Task<DesiredStateDiff> GetDesiredStateDiffAsync(string profileId, CancellationToken cancellationToken = default) =>
        _provisioning.GetDesiredStateDiffAsync(profileId, cancellationToken);

    public Task<ProvisioningPlan> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
        _provisioning.GetPlanAsync(profileId, cancellationToken);

    public Task<ProvisioningOperation> CreateProvisioningAsync(string profileId, CancellationToken cancellationToken = default) =>
        _provisioning.CreateAsync(profileId, cancellationToken);

    public string ConfirmProvisioning(string operationId) =>
        _provisioning.Confirm(operationId);

    public Task RunProvisioningAsync(
        string operationId,
        CancellationToken cancellationToken = default) =>
        _provisioning.RunAsync(operationId, cancellationToken);

    public ProvisioningOperation? GetProvisioningStatus(string operationId) =>
        _provisioning.Get(operationId);

    public string ResumeProvisioning(string operationId) =>
        _provisioning.Resume(operationId);

    public IReadOnlyList<ProvisioningOperation> GetProvisioningHistory() =>
        _provisioning.GetHistory();

    public ProvisioningOperation? GetProvisioningRecovery()
    {
        var latest = _provisioning.GetHistory()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        return latest?.Status is ProvisioningOperationStatuses.AwaitingConfirmation
            or ProvisioningOperationStatuses.Queued
            or ProvisioningOperationStatuses.Running
            or ProvisioningOperationStatuses.Failed
            ? latest
            : null;
    }

    public ProvisioningOperation? GetProvisioningDetail(string operationId) =>
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
