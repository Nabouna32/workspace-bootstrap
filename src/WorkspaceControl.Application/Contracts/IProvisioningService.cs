using WorkspaceBootstrap;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Application.Contracts;

public interface IProvisioningService
{
    IReadOnlyList<ProfileManifest> GetProfiles();
    Task<object> GetPlanAsync(string profileId, CancellationToken cancellationToken = default);
    string Start(string profileId, bool cacheOnly = false);
    Task RunAsync(string operationId, bool cacheOnly = false, CancellationToken cancellationToken = default);
    ProvisioningOperation? Get(string operationId);
    string Resume(string operationId, bool cacheOnly = false);
    IReadOnlyList<ProvisioningOperation> GetHistory();
}
