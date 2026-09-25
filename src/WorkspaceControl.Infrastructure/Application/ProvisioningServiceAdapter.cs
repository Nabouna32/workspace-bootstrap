using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Infrastructure.Application;

internal sealed class ProvisioningServiceAdapter : IProvisioningService
{
    private readonly ProvisioningEngine _engine;

    public ProvisioningServiceAdapter(ProvisioningEngine engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    public IReadOnlyList<WorkspaceManifest> GetWorkspaces() => _engine.Workspaces();
    public IReadOnlyList<ComponentManifest> GetApplicationCatalog() => _engine.ApplicationCatalog();
    public WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds) =>
        _engine.CreateWorkspace(name, description, componentIds);
    public void SaveWorkspace(WorkspaceManifest workspace) => _engine.SaveWorkspace(workspace);
    public Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _engine.DiffAsync(workspaceId, cancellationToken);
    public Task<ProvisioningPlan> GetPlanAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _engine.PlanAsync(workspaceId, cancellationToken);
    public Task<ProvisioningOperation> CreateAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        _engine.CreateAsync(workspaceId, cancellationToken);
    public string Confirm(string operationId) => _engine.Confirm(operationId);
    public Task RunAsync(string operationId, CancellationToken cancellationToken = default) =>
        _engine.RunAsync(operationId, cancellationToken);
    public ProvisioningOperation? Get(string operationId) => _engine.Get(operationId);
    public string Resume(string operationId) => _engine.Resume(operationId);
    public IReadOnlyList<ProvisioningOperation> GetHistory() => _engine.History().ToArray();
}
