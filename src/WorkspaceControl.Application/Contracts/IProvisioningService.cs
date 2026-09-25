using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface IProvisioningService
{
    IReadOnlyList<WorkspaceManifest> GetWorkspaces();
    IReadOnlyList<ComponentManifest> GetApplicationCatalog();
    WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds);
    void SaveWorkspace(WorkspaceManifest workspace);
    Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<ProvisioningPlan> GetPlanAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<ProvisioningOperation> CreateAsync(string workspaceId, CancellationToken cancellationToken = default);
    string Confirm(string operationId);
    Task RunAsync(string operationId, CancellationToken cancellationToken = default);
    ProvisioningOperation? Get(string operationId);
    string Resume(string operationId);
    IReadOnlyList<ProvisioningOperation> GetHistory();
}
