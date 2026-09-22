using System.Diagnostics;
using System.Text.Json;

namespace WorkspaceBootstrap;

public sealed class ProvisioningEngine
{
    private readonly ConfigurationStore _config;
    private readonly InstallerEngine _installer;
    private readonly WorkspacePaths _paths;

    public ProvisioningEngine(
        ConfigurationStore config,
        InstallerEngine installer,
        WorkspacePaths paths)
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
            Status = "queued",
            Total = profile.Components.Length
        };

        Save(operation);
        try
        {
            LaunchWorker(operation.OperationId, cacheOnly);
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
        bool cacheOnly,
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
                    await _installer.InstallAsync(component, cacheOnly, token);
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

    public string Resume(string id, bool cacheOnly)
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

    private void LaunchWorker(string operationId, bool cacheOnly)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Impossible de déterminer le processus Workspace Bootstrap.");

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
                Arguments = $"\"{entryAssembly}\" provisioning-worker --operation \"{operationId}\""
                    + (cacheOnly ? " --cache-only" : ""),
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = currentAssembly,
                Arguments = $"provisioning-worker --operation \"{operationId}\""
                    + (cacheOnly ? " --cache-only" : ""),
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
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
