using System.Diagnostics;
using System.Text.Json;

namespace WorkspaceControl.Infrastructure;

public sealed class ProvisioningEngine
{
    private readonly ConfigurationStore _config;
    private readonly InstallerEngine _installer;
    private readonly WorkspacePaths _paths;
    private readonly InventoryScanner _inventory;
    private readonly WindowsSystemService _windows;
    private readonly RegistryDesiredStateObserver _registry;
    private readonly RegistryDesiredStateWriter _registryWriter;

    public ProvisioningEngine(
        ConfigurationStore config,
        InstallerEngine installer,
        WorkspacePaths paths,
        InventoryScanner inventory,
        WindowsSystemService? windows = null,
        RegistryDesiredStateObserver? registry = null,
        RegistryDesiredStateWriter? registryWriter = null)
    {
        _config = config;
        _installer = installer;
        _paths = paths;
        _inventory = inventory;
        _windows = windows ?? new WindowsSystemService();
        _registry = registry ?? new RegistryDesiredStateObserver();
        _registryWriter = registryWriter ?? new RegistryDesiredStateWriter();
    }

    public IReadOnlyList<ProfileManifest> Profiles() =>
        _config.LoadProfiles().Values.OrderBy(x => x.Id).ToArray();

    public async Task<DesiredStateDiff> DiffAsync(
        string profileId,
        CancellationToken token = default)
    {
        var profile = GetProfile(profileId);
        var components = _config.LoadComponents();
        var inventory = await _inventory.ScanAsync(token);

        var requests = profile.ApplicationRequests;