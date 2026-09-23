using System.Diagnostics;
using System.Text.Json;

namespace WorkspaceControl.Infrastructure;

public sealed class ProvisioningEngine
{
    private readonly ConfigurationStore _config;
    private readonly InstallerEngine _installer;
    private readonly WorkspacePaths _paths;
    private readonly InventoryScanner _inventory;

    public ProvisioningEngine(
        ConfigurationStore config,
        InstallerEngine installer,
        WorkspacePaths paths,
        InventoryScanner inventory)
    {
        _config = config;
        _installer = installer;
        _paths = paths;
        _inventory = inventory;
    }

    public IReadOnlyList<ProfileManifest> Profiles() =>
        _config.LoadProfiles().Values.OrderBy(x => x.Id).ToArray();

    public async Task<object> PlanAsync(
        string profileId,
        CancellationToken token = default)
    {
        var profile = GetProfile(profileId);
        var components = _config.LoadComponents();
        var inventory = await _inventory.ScanAsync(token);

        return new
        {
            Profile = profile,
            InventoryScanId = inventory.ScanId,
            InventoryDiagnostics = inventory.ProviderDiagnostics,
            Items = profile.Components.Select(id =>
            {
                var component = components.TryGetValue(id, out var value)
                    ? value
                    : throw new InvalidOperationException($"Composant inconnu : {id}");

                var match = FindInventoryMatch(component, inventory.Items);
                return BuildPlanItem(component, match);
            }).ToArray()
        };
    }

    private static InventoryItem? FindInventoryMatch(
        ComponentManifest component,
        IReadOnlyList<InventoryItem> items)
    {
        if (string.IsNullOrWhiteSpace(component.PackageId))
            return null;

        return items.FirstOrDefault(item =>
            item.Observations.Any(observation =>
                string.Equals(observation.ProviderId, component.PackageId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(observation.Id, component.PackageId, StringComparison.OrdinalIgnoreCase) ||
                observation.ProviderId?.EndsWith($":{component.PackageId}", StringComparison.OrdinalIgnoreCase) == true));
    }

    private static object BuildPlanItem(ComponentManifest component, InventoryItem? match)
    {
        if (match is null)
        {
            return new
            {
                component.Id,
                component.Name,
                StateCode = "MISSING",
                ActionCode = "install",
                Message = "Composant absent de l'inventaire détecté."
            };
        }

        var updateAvailable = match.Evidence.Any(evidence =>
            evidence.Kind.Equals("available-update", StringComparison.OrdinalIgnoreCase));

        return new
        {
            component.Id,
            component.Name,
            StateCode = updateAvailable ? "OUTDATED" : "INSTALLED",
            ActionCode = updateAvailable ? "update" : "none",
            Message = updateAvailable
                ? $"Version installée : {match.Version ?? "inconnue"} ; une mise à jour est signalée par l'inventaire."
                : $"Version installée : {match.Version ?? "inconnue"}."
        };
    }

    public string Start(string profileId)
    {
        var profile = GetProfile(profileId);
        var operation = new ProvisioningOperation
        {
            ProfileId = profile.Id,
            Status = "queued",
            Total = profile.Components.Length
        };

        Save(operation);
        try
        {
            LaunchWorker(operation.OperationId);
        }
        catch (Exception ex)
        {
            operation.Status = "failed";
            operation.Error = $"Impossible de démarrer le worker : {ex.Message}";
            operation.CanResume = true;
            Save(operation);
            throw;
        }

        return operation.OperationId;
    }

    public async Task RunAsync(
        string operationId,
        CancellationToken token)
    {
        var operation = Get(operationId)
            ?? throw new InvalidOperationException("Opération introuvable.");

        if (operation.Status is "completed")
            return;

        await using var operationLock = await AcquireOperationLockAsync(token);

        // A worker may have been queued while another operation was running.
        // Re-read the operation after acquiring the process-wide lock so a stale
        // worker cannot overwrite the final state written by the active worker.
        operation = Get(operationId)
            ?? throw new InvalidOperationException("Opération introuvable.");

        if (operation.Status is "completed")
            return;

        var profile = GetProfile(operation.ProfileId);
        var components = _config.LoadComponents();

        try
        {
            operation.Status = "running";
            operation.Error = null;
            operation.CanResume = true;
            Save(operation);

            for (var index = operation.Completed; index < profile.Components.Length; index++)
            {
                token.ThrowIfCancellationRequested();

                var componentId = profile.Components[index];
                if (!components.TryGetValue(componentId, out var component))
                    throw new InvalidOperationException($"Composant inconnu : {componentId}");

                operation.CurrentComponentName = component.Name;
                Save(operation);

                try
                {
                    await _installer.InstallAsync(component, token);
                    operation.Steps.Add(
                        new ProvisioningStep(component.Id, component.Name, "completed"));
                    operation.Completed = index + 1;
                    operation.Error = null;
                    Save(operation);
                }
                catch (Exception ex)
                {
                    operation.Steps.Add(
                        new ProvisioningStep(component.Id, component.Name, "failed", ex.Message));
                    operation.Error = ex.Message;
                    operation.Status = "failed";
                    operation.CanResume = true;
                    Save(operation);
                    return;
                }
            }

            operation.Status = "completed";
            operation.CurrentComponentName = null;
            operation.CanResume = false;
            operation.Error = null;
            Save(operation);
        }
        catch (OperationCanceledException)
        {
            operation.Status = "failed";
            operation.Error = "Opération annulée.";
            operation.CanResume = true;
            Save(operation);
            throw;
        }
        catch (Exception ex)
        {
            operation.Status = "failed";
            operation.Error = ex.Message;
            operation.CanResume = true;
            Save(operation);
            throw;
        }
    }

    public ProvisioningOperation? Get(string id)
    {
        var path = OperationPath(id);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<ProvisioningOperation>(
                File.ReadAllText(path),
                JsonDefaults.Options)
            : null;
    }

    public IEnumerable<ProvisioningOperation> History() =>
        Directory.EnumerateFiles(_paths.StateRoot, "operation-*.json")
            .Select(path => JsonSerializer.Deserialize<ProvisioningOperation>(
                File.ReadAllText(path),
                JsonDefaults.Options))
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.UpdatedAt);

    public string Resume(string id)
    {
        var operation = Get(id)
            ?? throw new InvalidOperationException("Opération introuvable.");

        if (operation.Status != "failed" || !operation.CanResume)
            throw new InvalidOperationException(
                "Cette opération ne peut pas être reprise.");

        try
        {
            LaunchWorker(operation.OperationId, cacheOnly);
        }
        catch (Exception ex)
        {
            operation.Status = "failed";
            operation.Error = $"Impossible de redémarrer le worker : {ex.Message}";
            operation.CanResume = true;
            Save(operation);
            throw;
        }

        return operation.OperationId;
    }

    private void LaunchWorker(string operationId)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Impossible de déterminer le processus Workspace Control.");

        var currentAssembly = Environment.ProcessPath;
        var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location;

        ProcessStartInfo psi;
        if (Path.GetFileNameWithoutExtension(processPath)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entryAssembly))
                throw new InvalidOperationException(
                    "Le chemin de l'assembly CLI est introuvable pour le worker de développement.");

            psi = new ProcessStartInfo
            {
                FileName = processPath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(entryAssembly);
            psi.ArgumentList.Add("provisioning-worker");
            psi.ArgumentList.Add("--operation");
            psi.ArgumentList.Add(operationId);
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = currentAssembly,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("provisioning-worker");
            psi.ArgumentList.Add("--operation");
            psi.ArgumentList.Add(operationId);
        }

        var worker = Process.Start(psi)
            ?? throw new InvalidOperationException(
                "Impossible de démarrer le worker de provisioning.");

        worker.Dispose();
    }

    private async Task<FileStream> AcquireOperationLockAsync(CancellationToken token)
    {
        var path = Path.Combine(_paths.StateRoot, "provisioning.lock");

        while (true)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.WriteThrough);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), token);
            }
        }
    }

    private ProfileManifest GetProfile(string id) =>
        _config.LoadProfiles().TryGetValue(id, out var profile)
            ? profile
            : throw new InvalidOperationException($"Profil inconnu : {id}");

    private void Save(ProvisioningOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.OperationId))
            throw new InvalidOperationException("L'opération doit posséder un identifiant.");
        if (string.IsNullOrWhiteSpace(operation.ProfileId))
            throw new InvalidOperationException("L'opération doit référencer un profil.");
        if (operation.Completed < 0 || operation.Total < 0 || operation.Completed > operation.Total)
            throw new InvalidOperationException("L'état de progression de l'opération est incohérent.");

        Directory.CreateDirectory(_paths.StateRoot);
        operation.UpdatedAt = DateTimeOffset.UtcNow;

        var path = OperationPath(operation.OperationId);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(operation, JsonDefaults.Options));

        File.Move(temporary, path, overwrite: true);
    }

    private string OperationPath(string id) =>
        Path.Combine(_paths.StateRoot, $"operation-{id}.json");
}
