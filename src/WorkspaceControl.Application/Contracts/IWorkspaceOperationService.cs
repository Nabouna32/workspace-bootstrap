using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface IWorkspaceOperationService
{
    IReadOnlyList<WorkspaceManifest> GetWorkspaces();
    IReadOnlyList<ComponentManifest> GetApplicationCatalog();
    WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds);
    void SaveWorkspace(WorkspaceManifest workspace);
    Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspacePlan> GetPlanAsync(string workspaceId, CancellationToken cancellationToken = default);
    Task<WorkspaceOperation> CreateAsync(string workspaceId, CancellationToken cancellationToken = default);
    string Confirm(string operationId);
    Task RunAsync(string operationId, CancellationToken cancellationToken = default);
    WorkspaceOperation? Get(string operationId);
    string Resume(string operationId);
    IReadOnlyList<WorkspaceOperation> GetHistory();
}
