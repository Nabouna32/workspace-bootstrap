namespace WorkspaceBootstrap;

public sealed class EngineFacade
{
    private readonly WorkspacePaths _paths = new();
    private readonly ConfigurationStore _configuration;
    private readonly InstallerEngine _installer;
    private readonly ProvisioningEngine _provisioning;
    private readonly WindowsSystemService _system = new();
    private readonly WindowsOptimizationService _optimization = new();
    private readonly InventoryScanner _inventory;

    public EngineFacade()
    {
        _configuration = new ConfigurationStore();
        _installer = new InstallerEngine(_paths);
        _inventory = new InventoryScanner([
            new WindowsRegistryUninstallInventoryProvider(),
            new WinGetInventoryProvider()
        ]);
        _provisioning = new ProvisioningEngine(_configuration, _installer, _paths, _inventory);
    }

    public IReadOnlyList<ProfileManifest> GetProfiles() => _provisioning.Profiles();
    public Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
        _provisioning.PlanAsync(profileId, cancellationToken);
    public string StartProvisioning(string profileId, bool cacheOnly = false) => _provisioning.Start(profileId, cacheOnly);
    public Task RunProvisioningAsync(string operationId, bool cacheOnly = false, CancellationToken cancellationToken = default) =>
        _provisioning.RunAsync(operationId, cacheOnly, cancellationToken);
    public ProvisioningOperation? GetProvisioningStatus(string operationId) => _provisioning.Get(operationId);
    public string ResumeProvisioning(string operationId, bool cacheOnly = false) => _provisioning.Resume(operationId, cacheOnly);
    public IReadOnlyList<ProvisioningOperation> GetProvisioningHistory() => _provisioning.History().ToArray();

    public ProvisioningOperation? GetProvisioningRecovery() =>
        _provisioning.History()
            .Where(x => x.Status is "starting" or "running" or "failed")
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

    public ProvisioningOperation? GetProvisioningDetail(string operationId) =>
        _provisioning.Get(operationId);
    public Task<InventorySnapshot> GetInventoryAsync(CancellationToken cancellationToken = default) =>
        _inventory.ScanAsync(cancellationToken);
    public BaselineSnapshot GetBaseline() => _system.GetBaseline();
    public IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan() => _optimization.GetSafePlan();
    public IReadOnlyList<string> ApplySafeOptimization() => _optimization.ApplySafe();
    public IReadOnlyList<string> RollbackOptimization() => _optimization.Rollback();
}
