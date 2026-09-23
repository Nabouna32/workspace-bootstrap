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

    public async Task<ProvisioningPlan> PlanAsync(
        string profileId,
        CancellationToken token = default)
    {
        var profile = GetProfile(profileId);
        var components = _config.LoadComponents();
        var inventory = await _inventory.ScanAsync(token);

        var items = profile.Components.Select(id =>
        {
            var component = components.TryGetValue(id, out var value)
                ? value
                : throw new InvalidOperationException($"Unknown component: {id}");

            var match = FindInventoryMatch(component, inventory.Items);
            return BuildPlanItem(component, match, inventory.ProviderDiagnostics);
        }).ToArray();

        return new ProvisioningPlan(
            profile.Id,
            profile.Name,
            inventory.ScanId,
            inventory.ProviderDiagnostics,
            items,
            DateTimeOffset.UtcNow);
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

    private static ProvisioningPlanItem BuildPlanItem(
        ComponentManifest component,
        InventoryItem? match,
        IReadOnlyList<InventoryProviderDiagnostic> diagnostics)
    {
        if (match is null)
        {
            if (diagnostics.Any(diagnostic => !diagnostic.Success))
            {
                return new ProvisioningPlanItem(
                    component.Id,
                    component.Name,
                    ProvisioningStateCodes.Unknown,
                    ProvisioningActionCodes.Blocked,
                    null,
                    "Inventory is incomplete; Workspace Control cannot safely determine whether the component is installed.");
            }

            return new ProvisioningPlanItem(
                component.Id,
                component.Name,
                ProvisioningStateCodes.Missing,
                ProvisioningActionCodes.Install,
                null,
                "Component is absent from the detected inventory.");
        }

        var updateAvailable = match.Evidence.Any(evidence =>
            evidence.Kind.Equals("available-update", StringComparison.OrdinalIgnoreCase));

        return new ProvisioningPlanItem(
            component.Id,
            component.Name,
            updateAvailable
                ? ProvisioningStateCodes.Outdated
                : ProvisioningStateCodes.Installed,
            updateAvailable
                ? ProvisioningActionCodes.Update
                : ProvisioningActionCodes.None,
            match.Version,
            updateAvailable
                ? $"Installed version: {match.Version ?? "unknown"}; inventory reports an available update."
                : $"Installed version: {match.Version ?? "unknown"}.");
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
            operation.Error = $"Unable to start worker: {ex.Message}";
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
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status is "completed")
            return;

        await using var operationLock = await AcquireOperationLockAsync(token);

        operation = Get(operationId)
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status is "completed")
            return;

        var profile = GetProfile(operation.ProfileId);
        var components = _config.LoadComponents();

        try
        {
            var plan = await PlanAsync(operation.ProfileId, token);
            if (plan.Items.Count != profile.Components.Length)
            {
                throw new InvalidOperationException(
                    "Provisioning plan is inconsistent with the profile.");
            }

            operation.Status = "running";
            operation.Error = null;
            operation.CanResume = true;
            Save(operation);

            for (var index = operation.Completed; index < profile.Components.Length; index++)
            {
                token.ThrowIfCancellationRequested();

                var componentId = profile.Components[index];
                if (!components.TryGetValue(componentId, out var component))
                    throw new InvalidOperationException($"Unknown component: {componentId}");

                var planned = plan.Items[index];
                operation.CurrentComponentName = component.Name;
                Save(operation);

                if (planned.ActionCode == ProvisioningActionCodes.Blocked)
                {
                    throw new InvalidOperationException(planned.Message);
                }

                if (planned.ActionCode == ProvisioningActionCodes.None)
                {
                    operation.Steps.Add(
                        new ProvisioningStep(component.Id, component.Name, "skipped"));
                    operation.Completed = index + 1;
                    Save(operation);
                    continue;
                }

                try
                {
                    await _installer.InstallAsync(component, planned.ActionCode, token);

                    var verificationPlan = await PlanAsync(operation.ProfileId, token);
                    var verified = verificationPlan.Items[index];

                    if (verified.StateCode != ProvisioningStateCodes.Installed
                        || verified.ActionCode != ProvisioningActionCodes.None)
                    {
                        throw new InvalidOperationException(
                            $"Post-condition verification failed for '{component.Name}': {verified.Message}");
                    }

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
            operation.Error = "Operation cancelled.";
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
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status != "failed" || !operation.CanResume)
            throw new InvalidOperationException(
                "This operation cannot be resumed.");

        try
        {
            LaunchWorker(operation.OperationId);
        }
        catch (Exception ex)
        {
            operation.Status = "failed";
            operation.Error = $"Unable to restart worker: {ex.Message}";
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
                "Unable to determine the Workspace Control process.");

        var currentAssembly = Environment.ProcessPath;
        var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location;

        ProcessStartInfo psi;
        if (Path.GetFileNameWithoutExtension(processPath)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entryAssembly))
                throw new InvalidOperationException(
                    "The CLI assembly path is unavailable for the development worker.");

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
                "Unable to start the provisioning worker.");

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
            : throw new InvalidOperationException($"Unknown profile: {id}");

    private void Save(ProvisioningOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.OperationId))
            throw new InvalidOperationException("Operation must have an identifier.");
        if (string.IsNullOrWhiteSpace(operation.ProfileId))
            throw new InvalidOperationException("Operation must reference a profile.");
        if (operation.Completed < 0 || operation.Total < 0 || operation.Completed > operation.Total)
            throw new InvalidOperationException("Operation progress is inconsistent.");

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
