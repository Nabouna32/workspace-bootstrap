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
        ValidateDesiredStateSupport(profile);
        var components = _config.LoadComponents();
        var inventory = await _inventory.ScanAsync(token);

        var requests = profile.ApplicationRequests;
        if (requests.Count == 0)
            throw new InvalidOperationException($"Profile '{profile.Id}' does not declare any desired applications.");

        var items = requests.Select(request =>
        {
            if (!components.TryGetValue(request.ComponentId, out var catalogComponent))
                throw new InvalidOperationException($"Unknown component: {request.ComponentId}");

            var component = ApplyProfileOverrides(catalogComponent, request);
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
                    null,
                    null,
                    "Inventory is incomplete; Workspace Control cannot safely determine whether the component is installed.");
            }

            return new ProvisioningPlanItem(
                component.Id,
                component.Name,
                ProvisioningStateCodes.Missing,
                ProvisioningActionCodes.Install,
                null,
                null,
                null,
                "Component is absent from the detected inventory.");
        }

        var installedVersion = match.Version;
        var availableVersion = match.AvailableVersion;
        var policy = component.VersionPolicy?.Trim().ToLowerInvariant();

        if (policy == "minimum")
        {
            if (!TryParseVersion(component.MinimumVersion, out var minimumVersion))
            {
                return new ProvisioningPlanItem(
                    component.Id,
                    component.Name,
                    ProvisioningStateCodes.Unknown,
                    ProvisioningActionCodes.Blocked,
                    installedVersion,
                    availableVersion,
                    component.MinimumVersion,
                    "The component declares a minimum-version policy without a valid minimum version.");
            }

            if (!TryParseVersion(installedVersion, out var installed)
                || installed < minimumVersion)
            {
                return new ProvisioningPlanItem(
                    component.Id,
                    component.Name,
                    ProvisioningStateCodes.Outdated,
                    ProvisioningActionCodes.Update,
                    installedVersion,
                    availableVersion,
                    component.MinimumVersion,
                    $"Installed version: {installedVersion ?? "unknown"}; minimum required version: {component.MinimumVersion}.");
            }

            return new ProvisioningPlanItem(
                component.Id,
                component.Name,
                ProvisioningStateCodes.Installed,
                ProvisioningActionCodes.None,
                installedVersion,
                availableVersion,
                component.MinimumVersion,
                $"Installed version: {installedVersion}; minimum required version: {component.MinimumVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(policy) && policy != "latest-stable" && policy != "stable-compatible")
        {
            return new ProvisioningPlanItem(
                component.Id,
                component.Name,
                ProvisioningStateCodes.Unknown,
                ProvisioningActionCodes.Blocked,
                installedVersion,
                availableVersion,
                null,
                $"Unsupported version policy '{component.VersionPolicy}'.");
        }

        if (!string.IsNullOrWhiteSpace(availableVersion))
        {
            var policyDescription = policy == "stable-compatible"
                ? "stable-compatible available"
                : "latest stable available";

            return new ProvisioningPlanItem(
                component.Id,
                component.Name,
                ProvisioningStateCodes.Outdated,
                ProvisioningActionCodes.Update,
                installedVersion,
                availableVersion,
                availableVersion,
                $"Installed version: {installedVersion ?? "unknown"}; {policyDescription} version: {availableVersion}.");
        }

        return new ProvisioningPlanItem(
            component.Id,
            component.Name,
            ProvisioningStateCodes.Installed,
            ProvisioningActionCodes.None,
            installedVersion,
            null,
            null,
            $"Installed version: {installedVersion ?? "unknown"}.");
    }

    private static void ValidatePostcondition(
        ComponentManifest component,
        ProvisioningPlanItem planned,
        ProvisioningPlanItem verified)
    {
        if (verified.StateCode != ProvisioningStateCodes.Installed
            || verified.ActionCode != ProvisioningActionCodes.None)
        {
            throw new InvalidOperationException(
                $"Post-condition verification failed for '{component.Name}': {verified.Message}");
        }

        if (string.IsNullOrWhiteSpace(verified.InstalledVersion))
        {
            throw new InvalidOperationException(
                $"Post-condition verification failed for '{component.Name}': the installed version is unavailable.");
        }

        if (string.Equals(component.VersionPolicy, "minimum", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseVersion(planned.DesiredVersion, out var minimum)
                || !TryParseVersion(verified.InstalledVersion, out var installed)
                || installed < minimum)
            {
                throw new InvalidOperationException(
                    $"Post-condition verification failed for '{component.Name}': installed version '{verified.InstalledVersion}' does not satisfy minimum version '{planned.DesiredVersion}'.");
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(planned.DesiredVersion))
        {
            if (!TryParseVersion(planned.DesiredVersion, out var desired)
                || !TryParseVersion(verified.InstalledVersion, out var installed)
                || installed != desired)
            {
                throw new InvalidOperationException(
                    $"Post-condition verification failed for '{component.Name}': installed version '{verified.InstalledVersion}' does not match confirmed target '{planned.DesiredVersion}'.");
            }
        }
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        if (Version.TryParse(value, out var parsed) && parsed is not null)
        {
            version = parsed;
            return true;
        }

        version = default!;
        return false;
    }

    public async Task<ProvisioningOperation> CreateAsync(
        string profileId,
        CancellationToken token = default)
    {
        var plan = await PlanAsync(profileId, token);
        var operation = new ProvisioningOperation
        {
            ProfileId = plan.ProfileId,
            Plan = plan,
            Status = ProvisioningOperationStatuses.AwaitingConfirmation,
            Total = plan.Items.Count,
            CanResume = false
        };

        Save(operation);
        return operation;
    }

    public string Confirm(string operationId)
    {
        var operation = Get(operationId)
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status != ProvisioningOperationStatuses.AwaitingConfirmation)
            throw new InvalidOperationException(
                "Only an operation awaiting confirmation can be confirmed.");

        if (operation.Plan is null)
            throw new InvalidOperationException(
                "The operation does not contain a provisioning plan.");

        if (operation.Plan.Items.Any(item => item.ActionCode == ProvisioningActionCodes.Blocked))
            throw new InvalidOperationException(
                "The provisioning plan contains blocked actions and cannot be confirmed.");

        operation.Status = ProvisioningOperationStatuses.Queued;
        operation.CanResume = false;
        operation.Error = null;
        Save(operation);

        try
        {
            LaunchWorker(operation.OperationId);
        }
        catch (Exception ex)
        {
            operation.Status = ProvisioningOperationStatuses.Failed;
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

        if (operation.Status is ProvisioningOperationStatuses.Completed)
            return;

        await using var operationLock = await AcquireOperationLockAsync(token);

        operation = Get(operationId)
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status is ProvisioningOperationStatuses.Completed)
            return;

        var profile = GetProfile(operation.ProfileId);
        ValidateDesiredStateSupport(profile);
        var components = _config.LoadComponents();
        var requests = profile.ApplicationRequests;
        var plan = operation.Plan
            ?? throw new InvalidOperationException(
                "The operation has no persisted provisioning plan.");

        if (operation.Status is ProvisioningOperationStatuses.AwaitingConfirmation
            or ProvisioningOperationStatuses.Stale
            or ProvisioningOperationStatuses.Completed)
            throw new InvalidOperationException(
                "The provisioning operation is not in an executable state.");

        if (plan.Items.Count != requests.Count)
            throw new InvalidOperationException(
                "Persisted provisioning plan is inconsistent with the current profile.");

        try
        {
            var currentPlan = await PlanAsync(operation.ProfileId, token);
            ValidateUncompletedPlanState(operation, plan, currentPlan, requests);

            operation.Status = ProvisioningOperationStatuses.Running;
            operation.Error = null;
            operation.CanResume = true;
            Save(operation);

            for (var index = operation.Completed; index < requests.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                var request = requests[index];
                if (!components.TryGetValue(request.ComponentId, out var catalogComponent))
                    throw new InvalidOperationException($"Unknown component: {request.ComponentId}");
                var component = ApplyProfileOverrides(catalogComponent, request);

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
                    await _installer.InstallAsync(
                        component,
                        planned.ActionCode,
                        string.Equals(component.VersionPolicy, "minimum", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : planned.DesiredVersion,
                        token);

                    operation.CurrentComponentName = $"Verifying: {component.Name}";
                    Save(operation);
                    var verificationPlan = await PlanAsync(operation.ProfileId, token);
                    var verified = verificationPlan.Items[index];

                    ValidatePostcondition(component, planned, verified);

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
                    operation.Status = ProvisioningOperationStatuses.Failed;
                    operation.CanResume = true;
                    Save(operation);
                    return;
                }
            }

            operation.CurrentComponentName = "Verifying final state…";
            Save(operation);

            var finalPlan = await PlanAsync(operation.ProfileId, token);
            for (var index = 0; index < plan.Items.Count; index++)
            {
                var planned = plan.Items[index];
                var verified = finalPlan.Items[index];

                if (planned.ActionCode == ProvisioningActionCodes.None)
                {
                    if (verified.StateCode != ProvisioningStateCodes.Installed
                        || verified.ActionCode != ProvisioningActionCodes.None)
                    {
                        throw new InvalidOperationException(
                            $"Final state verification failed for '{planned.ComponentName}': {verified.Message}");
                    }

                    continue;
                }

                var request = requests[index];
                if (!components.TryGetValue(request.ComponentId, out var catalogComponent))
                    throw new InvalidOperationException($"Unknown component: {request.ComponentId}");
                var component = ApplyProfileOverrides(catalogComponent, request);

                ValidatePostcondition(component, planned, verified);
            }

            operation.Status = ProvisioningOperationStatuses.Completed;
            operation.CurrentComponentName = null;
            operation.CanResume = false;
            operation.Error = null;
            Save(operation);
        }
        catch (OperationCanceledException)
        {
            operation.Status = ProvisioningOperationStatuses.Failed;
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

        if (operation.Status != ProvisioningOperationStatuses.Failed || !operation.CanResume)
            throw new InvalidOperationException(
                "This operation cannot be resumed.");

        operation.Status = ProvisioningOperationStatuses.Queued;
        operation.CanResume = false;
        Save(operation);

        try
        {
            LaunchWorker(operation.OperationId);
        }
        catch (Exception ex)
        {
            operation.Status = ProvisioningOperationStatuses.Failed;
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

    private static ComponentManifest ApplyProfileOverrides(
        ComponentManifest component,
        ProfileApplication request)
    {
        if (request.VersionPolicy is null && request.MinimumVersion is null)
            return component;

        return component with
        {
            VersionPolicy = request.VersionPolicy ?? component.VersionPolicy,
            MinimumVersion = request.MinimumVersion ?? component.MinimumVersion
        };
    }

    private static void ValidateDesiredStateSupport(ProfileManifest profile)
    {
        if (profile.HasUnsupportedDesiredStateSections)
        {
            throw new InvalidOperationException(
                $"Profile '{profile.Id}' contains desired-state sections that are not executable yet. " +
                "Only applications are currently supported by the provisioning engine.");
        }
    }

    private ProfileManifest GetProfile(string id) =>
        _config.LoadProfiles().TryGetValue(id, out var profile)
            ? profile
            : throw new InvalidOperationException($"Unknown profile: {id}");

    private static void ValidateUncompletedPlanState(
        ProvisioningOperation operation,
        ProvisioningPlan persistedPlan,
        ProvisioningPlan currentPlan,
        IReadOnlyList<ProfileApplication> requests)
    {
        for (var index = operation.Completed; index < requests.Count; index++)
        {
            var expected = persistedPlan.Items[index];
            var observed = currentPlan.Items[index];

            if (!string.Equals(expected.ComponentId, observed.ComponentId, StringComparison.Ordinal)
                || !string.Equals(expected.StateCode, observed.StateCode, StringComparison.Ordinal)
                || !string.Equals(expected.ActionCode, observed.ActionCode, StringComparison.Ordinal)
                || !string.Equals(expected.InstalledVersion, observed.InstalledVersion, StringComparison.Ordinal)
                || !string.Equals(expected.AvailableVersion, observed.AvailableVersion, StringComparison.Ordinal)
                || !string.Equals(expected.DesiredVersion, observed.DesiredVersion, StringComparison.Ordinal))
            {
                operation.Status = ProvisioningOperationStatuses.Stale;
                operation.CanResume = false;
                operation.Error =
                    $"Provisioning plan is stale for '{expected.ComponentName}'. " +
                    $"Observed state is {observed.StateCode}/{observed.ActionCode} " +
                    $"but the confirmed plan expected {expected.StateCode}/{expected.ActionCode}.";
                throw new InvalidOperationException(operation.Error);
            }
        }
    }

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
