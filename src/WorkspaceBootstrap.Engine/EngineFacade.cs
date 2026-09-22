namespace WorkspaceBootstrap;

public sealed class EngineFacade
{
    private readonly WorkspacePaths _paths = new();
    private readonly ConfigurationStore _configuration;
    private readonly InstallerEngine _installer;
    private readonly ProvisioningEngine _provisioning;
    private readonly WindowsSystemService _system = new();
    private readonly WindowsOptimizationService _optimization = new();

    public EngineFacade()
    {
        _configuration = new ConfigurationStore();
        _installer = new InstallerEngine(_paths);
        _provisioning = new ProvisioningEngine(_configuration, _installer, _paths);
    }

    public IReadOnlyList<ProfileManifest> GetProfiles() => _provisioning.Profiles();
    public object GetPlan(string profileId) => _provisioning.Plan(profileId);
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
    public BaselineSnapshot GetBaseline() => _system.GetBaseline();
    public IReadOnlyList<OptimizationPlanItem> GetSafeOptimizationPlan() => _optimization.GetSafePlan();
    public IReadOnlyList<string> ApplySafeOptimization() => _optimization.ApplySafe();
    public IReadOnlyList<string> RollbackOptimization() => _optimization.Rollback();
}
