using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface IProvisioningService
{
    IReadOnlyList<WorkspaceManifest> GetProfiles();
    IReadOnlyList<ComponentManifest> GetApplicationCatalog();
    WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds);
    void SaveWorkspace(WorkspaceManifest workspace);
    Task<DesiredStateDiff> GetDesiredStateDiffAsync(string profileId, CancellationToken cancellationToken = default);
    Task<ProvisioningPlan> GetPlanAsync(string profileId, CancellationToken cancellationToken = default);
    Task<ProvisioningOperation> CreateAsync(string profileId, CancellationToken cancellationToken = default);
    string Confirm(string operationId);
    Task RunAsync(string operationId, CancellationToken cancellationToken = default);
    ProvisioningOperation? Get(string operationId);
    string Resume(string operationId);
    IReadOnlyList<ProvisioningOperation> GetHistory();
}
