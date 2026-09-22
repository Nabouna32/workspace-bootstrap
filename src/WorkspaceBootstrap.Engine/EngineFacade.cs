using WorkspaceControl.Application;

namespace WorkspaceBootstrap;

public sealed class EngineFacade
{
    private readonly IWorkspaceControlApplication _application;

    public EngineFacade()
    {
        var paths = new WorkspacePaths();
        var configuration = new ConfigurationStore();
        var installer = new InstallerEngine(paths);
        var inventory = new InventoryScanner([
            new WindowsRegistryUninstallInventoryProvider(),
            new WinGetInventoryProvider()
        ]);
        var provisioning = new ProvisioningEngine(configuration, installer, paths, inventory);

        _application = new WorkspaceControlApplication(
            new Application.InventoryServiceAdapter(inventory),
            new Application.ProvisioningServiceAdapter(provisioning),
            new Application.WindowsAdministrationServiceAdapter(),
            new Application.OptimizationServiceAdapter());
    }

    public IReadOnlyList<ProfileManifest> GetProfiles() => _application.GetProfiles();

    public Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
        _application.GetPlanAsync(profileId, cancellationToken);

    public string StartProvisioning(string profileId, bool cacheOnly = false) =>
        _application.StartProvisioning(profileId, cacheOnly);

    public Task RunProvisioningAsync(
        string operationId,
        bool cacheOnly = false,
        CancellationToken cancellationToken = default) =>
        _application.RunProvisioningAsync(operationId, cacheOnly, cancellationToken);

    public ProvisioningOperation? GetProvisioningStatus(string operationId) =>
        _application.GetProvisioningStatus(operationId);

    public string ResumeProvisioning(string operationId, bool cacheOnly = false) =>
        _application.ResumeProvisioning(operationId, cacheOnly);

    public IReadOnlyList<ProvisioningOperation> GetProvisioningHistory() =>
        _application.GetProvisioningHistory();

    public ProvisioningOperation? GetProvisioningRecovery() =>
        _application.GetProvisioningRecovery();

    public ProvisioningOperation? GetProvisioningDetail(string operationId) =>
        _application.GetProvisioningDetail(operationId);

    public Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        _application.GetInventoryAsync(cancellationToken);

    public BaselineSnapshot GetBaseline() => _application.GetBaseline();

    public IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan() =>
        _application.GetSafeOptimizationPlan();

    public IReadOnlyList<string> ApplySafeOptimization() =>
        _application.ApplySafeOptimization();

    public IReadOnlyList<string> RollbackOptimization() =>
        _application.RollbackOptimization();
}
