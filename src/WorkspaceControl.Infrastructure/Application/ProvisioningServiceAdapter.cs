using WorkspaceControl.Application.Contracts;

namespace WorkspaceBootstrap.Application;

internal sealed class ProvisioningServiceAdapter : IProvisioningService
{
    private readonly ProvisioningEngine _engine;

    public ProvisioningServiceAdapter(ProvisioningEngine engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public IReadOnlyList<ProfileManifest> GetProfiles() => _engine.Profiles();
    public Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
        _engine.PlanAsync(profileId, cancellationToken);
    public string Start(string profileId, bool cacheOnly = false) => _engine.Start(profileId, cacheOnly);
    public Task RunAsync(string operationId, bool cacheOnly = false, CancellationToken cancellationToken = default) =>
        _engine.RunAsync(operationId, cacheOnly, cancellationToken);
    public ProvisioningOperation? Get(string operationId) => _engine.Get(operationId);
    public string Resume(string operationId, bool cacheOnly = false) => _engine.Resume(operationId, cacheOnly);
    public IReadOnlyList<ProvisioningOperation> GetHistory() => _engine.History().ToArray();
}
