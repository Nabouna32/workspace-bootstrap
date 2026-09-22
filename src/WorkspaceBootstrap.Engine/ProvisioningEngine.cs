using System.Text.Json;

namespace WorkspaceBootstrap;

public sealed class ProvisioningEngine
{
    private readonly ConfigurationStore _config;
    private readonly InstallerEngine _installer;
    private readonly WorkspacePaths _paths;

    public ProvisioningEngine(ConfigurationStore config, InstallerEngine installer, WorkspacePaths paths)
    {
        _config = config;
        _installer = installer;
        _paths = paths;
    }

    public IReadOnlyList<ProfileManifest> Profiles() =>
        _config.LoadProfiles().Values.OrderBy(x => x.Id).ToArray();

    public object Plan(string profileId)
    {
        var profile = GetProfile(profileId);
        var components = _config.LoadComponents();

        return new
        {
            Profile = profile,
            Items = profile.Components.Select(id =>
            {
                var component = components.TryGetValue(id, out var value)
                    ? value
                    : throw new InvalidOperationException($"Composant inconnu : {id}");
                return new
                {
                    component.Id,
                    component.Name,
                    Status = "PENDING",
                    Action = "INSTALL_OR_VERIFY"
                };
            }).ToArray()
        };
    }

    public string Start(string profileId, bool cacheOnly)
    {
        var profile = GetProfile(profileId);
        var operation = new ProvisioningOperation
        {
            ProfileId = profile.Id,
            Status = "starting",
            Total = profile.Components.Length
        };

        Save(operation);

        _ = Task.Run(async () =>
        {
            try
            {
                var components = _config.LoadComponents();
                foreach (var componentId in profile.Components)
                {
                    var component = components[componentId];
                    operation.Status = "running";
                    operation.CurrentComponentName = component.Name;
                    Save(operation);

                    try
                    {
                        await _installer.InstallAsync(component, cacheOnly, CancellationToken.None);
                        operation.Steps.Add(new ProvisioningStep(component.Id, component.Name, "completed"));
                        operation.Completed++;
                    }
                    catch (Exception ex)
                    {
                        operation.Steps.Add(new ProvisioningStep(component.Id, component.Name, "failed", ex.Message));
                        operation.Error = ex.Message;
                        operation.Status = "failed";
                        operation.CanResume = true;
                        Save(operation);
                        return;
                    }

                    Save(operation);
                }

                operation.Status = "completed";
                operation.CurrentComponentName = null;
                operation.CanResume = false;
                Save(operation);
            }
            catch (Exception ex)
            {
                operation.Status = "failed";
                operation.Error = ex.Message;
                Save(operation);
            }
        });

        return operation.OperationId;
    }

    public ProvisioningOperation? Get(string id)
    {
        var path = OperationPath(id);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ProvisioningOperation>(File.ReadAllText(path), JsonDefaults.Options)
            : null;
    }

    public IEnumerable<ProvisioningOperation> History() =>
        Directory.EnumerateFiles(_paths.StateRoot, "operation-*.json")
            .Select(path => JsonSerializer.Deserialize<ProvisioningOperation>(
                File.ReadAllText(path), JsonDefaults.Options))
            .Where(x => x is not null)!
            .OrderByDescending(x => x!.UpdatedAt);

    public string Resume(string id, bool cacheOnly)
    {
        var previous = Get(id) ?? throw new InvalidOperationException("Opération introuvable.");
        if (previous.Status != "failed" || !previous.CanResume)
            throw new InvalidOperationException("Cette opération ne peut pas être reprise.");

        return Start(previous.ProfileId, cacheOnly);
    }

    private ProfileManifest GetProfile(string id) =>
        _config.LoadProfiles().TryGetValue(id, out var profile)
            ? profile
            : throw new InvalidOperationException($"Profil inconnu : {id}");

    private void Save(ProvisioningOperation operation)
    {
        operation.UpdatedAt = DateTimeOffset.UtcNow;
        File.WriteAllText(
            OperationPath(operation.OperationId),
            JsonSerializer.Serialize(operation, JsonDefaults.Options));
    }

    private string OperationPath(string id) =>
        Path.Combine(_paths.StateRoot, $"operation-{id}.json");
}
