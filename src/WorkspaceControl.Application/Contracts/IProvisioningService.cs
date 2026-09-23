using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface IProvisioningService
{
    IReadOnlyList<ProfileManifest> GetProfiles();
    Task<ProvisioningPlan> GetPlanAsync(string profileId, CancellationToken cancellationToken = default);
    string Start(string profileId);
    Task RunAsync(string operationId, CancellationToken cancellationToken = default);
    ProvisioningOperation? Get(string operationId);
    string Resume(string operationId);
    IReadOnlyList<ProvisioningOperation> GetHistory();
}
