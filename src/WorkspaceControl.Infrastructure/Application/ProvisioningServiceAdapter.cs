using WorkspaceControl.Application.Contracts;

namespace WorkspaceControl.Infrastructure.Application;

internal sealed class ProvisioningServiceAdapter : IProvisioningService
{
    private readonly ProvisioningEngine _engine;

    public ProvisioningServiceAdapter(ProvisioningEngine engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public IReadOnlyList<ProfileManifest> GetProfiles() => _engine.Profiles();
    public Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
        _engine.PlanAsync(profileId, cancellationToken);
    public string Start(string profileId) => _engine.Start(profileId);
    public Task RunAsync(string operationId, CancellationToken cancellationToken = default) =>
        _engine.RunAsync(operationId, cancellationToken);
    public ProvisioningOperation? Get(string operationId) => _engine.Get(operationId);
    public string Resume(string operationId) => _engine.Resume(operationId);
    public IReadOnlyList<ProvisioningOperation> GetHistory() => _engine.History().ToArray();
}
