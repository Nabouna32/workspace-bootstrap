using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceBootstrap.Infrastructure;

internal sealed class RegistrySoftwareInventorySource : ISoftwareInventorySource
{
    private readonly WindowsRegistryUninstallInventoryProvider _provider = new();

    public string Id => _provider.Id;

    public async Task<SoftwareInventorySourceResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var result = await _provider.ScanAsync(cancellationToken);

        return new SoftwareInventorySourceResult(
            result.Observations.Select(Map).ToArray(),
            result.Diagnostic.Success
                ? []
                : [result.Diagnostic.Message]);
    }

    private static SoftwareItem Map(InventoryObservation observation) =>
        new(
            observation.Id,
            observation.DisplayName,
            observation.Version,
            observation.Publisher,
            observation.InstallLocation,
            observation.Ownership switch
            {
                InventoryOwnership.System => SoftwareOwnership.System,
                InventoryOwnership.ProductManaged => SoftwareOwnership.ProductManaged,
                InventoryOwnership.PackageManagerManaged => SoftwareOwnership.PackageManagerManaged,
                InventoryOwnership.Manual => SoftwareOwnership.Manual,
                _ => SoftwareOwnership.Unknown
            },
            observation.Scope switch
            {
                InventoryScope.User => SoftwareScope.User,
                InventoryScope.System => SoftwareScope.System,
                _ => SoftwareScope.Unknown
            },
            [],
            observation.Evidence.Select(evidence =>
                new SoftwareEvidence(
                    evidence.Source ?? evidence.Kind,
                    evidence.Kind,
                    evidence.Description,
                    DateTimeOffset.UtcNow,
                    evidence.IsStrong))
            .ToArray());
}
