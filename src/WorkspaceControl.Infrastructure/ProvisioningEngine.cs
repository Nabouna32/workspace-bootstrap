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

        var items = requests.Select(request =>
        {
            if (!components.TryGetValue(request.ComponentId, out var catalogComponent))
                throw new InvalidOperationException($"Unknown component: {request.ComponentId}");

            var component = ApplyProfileOverrides(catalogComponent, request);
            var match = FindInventoryMatch(component, inventory.Items);
            var planItem = BuildPlanItem(component, match, inventory.ProviderDiagnostics);

            return new DesiredStateDiffItem(
                DesiredStateDomainCodes.Application,
                planItem.ComponentId,
                planItem.ComponentName,
                planItem.StateCode,
                planItem.ActionCode,
                planItem.InstalledVersion,
                planItem.AvailableVersion,
                planItem.DesiredVersion,
                planItem.Message);
        }).ToList();

        AddUnsupportedDesiredStateItems(profile, items);
        AddConditionDiffItems(profile, items);
        AddRegistryDiffItems(profile, items);

        return new DesiredStateDiff(
            profile.Id,
            profile.Name,
            inventory.ScanId,
            inventory.ProviderDiagnostics,
            items,
            DateTimeOffset.UtcNow);
    }

    public async Task<ProvisioningPlan> PlanAsync(
        string profileId,
        CancellationToken token = default)
    {
        var diff = await DiffAsync(profileId, token);

        var unsupported = diff.Items
            .Where(item =>
                item.Domain != DesiredStateDomainCodes.Application &&
                item.ActionCode == ProvisioningActionCodes.Blocked)
            .ToArray();

        if (unsupported.Length > 0)
        {
            throw new InvalidOperationException(
                $"Profile '{diff.ProfileId}' contains desired-state sections that are not executable yet. " +
                "The diff identifies these sections explicitly; no mutation plan was created.");
        }

        var items = diff.Items
            .Where(item => item.Domain != DesiredStateDomainCodes.Condition)
            .Select(item => new ProvisioningPlanItem(
                item.TargetId,
                item.TargetName,
                item.StateCode,
                item.ActionCode,
                item.ObservedValue,
                item.AvailableValue,
                item.DesiredValue,
                item.Message,
                item.Domain,
                item.TargetId))
            .ToArray();

        return new ProvisioningPlan(
            diff.ProfileId,
            diff.ProfileName,
            diff.InventoryScanId,
            diff.InventoryDiagnostics,
            items,
            diff.CreatedAtUtc);
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
        var components = _config.LoadComponents();
        var plan = operation.Plan
            ?? throw new InvalidOperationException(
                "The operation has no persisted provisioning plan.");

        if (operation.Status is ProvisioningOperationStatuses.AwaitingConfirmation
            or ProvisioningOperationStatuses.Stale
            or ProvisioningOperationStatuses.Completed)
            throw new InvalidOperationException(
                "The provisioning operation is not in an executable state.");

        try
        {
            var currentPlan = await PlanAsync(operation.ProfileId, token);
            ValidateUncompletedPlanState(operation, plan, currentPlan);

            operation.Status = ProvisioningOperationStatuses.Running;
            operation.Error = null;
            operation.CanResume = true;
            Save(operation);

            for (var index = operation.Completed; index < plan.Items.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                var planned = plan.Items[index];
                operation.CurrentComponentName = planned.ComponentName;
                Save(operation);

                if (planned.ActionCode == ProvisioningActionCodes.Blocked)
                    throw new InvalidOperationException(planned.Message);

                if (planned.ActionCode == ProvisioningActionCodes.None)
                {
                    operation.Steps.Add(
                        new ProvisioningStep(
                            planned.TargetId ?? planned.ComponentId,
                            planned.ComponentName,
                            "skipped"));
                    operation.Completed = index + 1;
                    Save(operation);
                    continue;
                }

                try
                {
                    if (string.Equals(
                            planned.Domain,
                            DesiredStateDomainCodes.RegistrySetting,
                            StringComparison.Ordinal))
                    {
                        var desired = FindRegistryDesired(profile, planned.TargetId ?? planned.ComponentId);
                        var snapshot = _registryWriter.Capture(desired);

                        operation.RegistrySnapshots.Add(snapshot with { PlanIndex = index });
                        Save(operation);

                        _registryWriter.Write(desired);
                    }
                    else if (string.Equals(
                                 planned.Domain,
                                 DesiredStateDomainCodes.Application,
                                 StringComparison.Ordinal))
                    {
                        if (!components.TryGetValue(planned.ComponentId, out var catalogComponent))
                            throw new InvalidOperationException($"Unknown component: {planned.ComponentId}");

                        var request = profile.ApplicationRequests
                            .FirstOrDefault(item =>
                                string.Equals(item.ComponentId, planned.ComponentId, StringComparison.Ordinal))
                            ?? throw new InvalidOperationException(
                                $"Profile '{profile.Id}' does not contain application '{planned.ComponentId}'.");

                        var component = ApplyProfileOverrides(catalogComponent, request);

                        await _installer.InstallAsync(
                            component,
                            planned.ActionCode,
                            string.Equals(component.VersionPolicy, "minimum", StringComparison.OrdinalIgnoreCase)
                                ? null
                                : planned.DesiredVersion,
                            token);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Unsupported executable desired-state domain '{planned.Domain}'.");
                    }

                    operation.CurrentComponentName = $"Verifying: {planned.ComponentName}";
                    Save(operation);

                    var verificationPlan = await PlanAsync(operation.ProfileId, token);
                    var verified = verificationPlan.Items
                        .FirstOrDefault(item =>
                            string.Equals(item.TargetId, planned.TargetId, StringComparison.Ordinal)
                            && string.Equals(item.Domain, planned.Domain, StringComparison.Ordinal))
                        ?? throw new InvalidOperationException(
                            $"Post-condition verification target '{planned.TargetId}' was not observed.");

                    if (string.Equals(
                            planned.Domain,
                            DesiredStateDomainCodes.RegistrySetting,
                            StringComparison.Ordinal))
                    {
                        ValidateRegistryPostcondition(planned, verified);
                    }
                    else
                    {
                        if (!components.TryGetValue(planned.ComponentId, out var catalogComponent))
                            throw new InvalidOperationException($"Unknown component: {planned.ComponentId}");

                        var request = profile.ApplicationRequests
                            .FirstOrDefault(item =>
                                string.Equals(item.ComponentId, planned.ComponentId, StringComparison.Ordinal))
                            ?? throw new InvalidOperationException(
                                $"Profile '{profile.Id}' does not contain application '{planned.ComponentId}'.");

                        ValidatePostcondition(
                            ApplyProfileOverrides(catalogComponent, request),
                            planned,
                            verified);
                    }

                    operation.Steps.Add(
                        new ProvisioningStep(
                            planned.TargetId ?? planned.ComponentId,
                            planned.ComponentName,
                            "completed"));
                    operation.Completed = index + 1;
                    operation.Error = null;
                    Save(operation);
                }
                catch (Exception ex)
                {
                    operation.Steps.Add(
                        new ProvisioningStep(
                            planned.TargetId ?? planned.ComponentId,
                            planned.ComponentName,
                            "failed",
                            ex.Message));

                    try
                    {
                        RollbackRegistrySnapshots(operation);
                    }
                    catch (Exception rollbackEx)
                    {
                        operation.Error = $"{ex.Message} Registry rollback also failed: {rollbackEx.Message}";
                        operation.Status = ProvisioningOperationStatuses.Failed;
                        operation.CanResume = false;
                        Save(operation);
                        return;
                    }

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
            if (finalPlan.Items.Count != plan.Items.Count)
                throw new InvalidOperationException(
                    "Final state verification failed because the desired-state item set changed.");

            foreach (var planned in plan.Items)
            {
                var verified = finalPlan.Items.FirstOrDefault(item =>
                    string.Equals(item.TargetId, planned.TargetId, StringComparison.Ordinal)
                    && string.Equals(item.Domain, planned.Domain, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        $"Final state verification target '{planned.TargetId}' was not observed.");

                if (string.Equals(
                        planned.Domain,
                        DesiredStateDomainCodes.RegistrySetting,
                        StringComparison.Ordinal))
                {
                    ValidateRegistryPostcondition(planned, verified);
                    continue;
                }

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

                if (!components.TryGetValue(planned.ComponentId, out var catalogComponent))
                    throw new InvalidOperationException($"Unknown component: {planned.ComponentId}");

                var request = profile.ApplicationRequests
                    .FirstOrDefault(item =>
                        string.Equals(item.ComponentId, planned.ComponentId, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        $"Profile '{profile.Id}' does not contain application '{planned.ComponentId}'.");

                ValidatePostcondition(
                    ApplyProfileOverrides(catalogComponent, request),
                    planned,
                    verified);
            }

            operation.Status = ProvisioningOperationStatuses.Completed;
            operation.CurrentComponentName = null;
            operation.CanResume = false;
            operation.Error = null;
            Save(operation);
        }
        catch (OperationCanceledException)
        {
            try
            {
                RollbackRegistrySnapshots(operation);
            }
            catch (Exception rollbackEx)
            {
                operation.Error = $"Operation cancelled. Registry rollback failed: {rollbackEx.Message}";
                operation.Status = ProvisioningOperationStatuses.Failed;
                operation.CanResume = false;
                Save(operation);
                throw;
            }

            operation.Status = ProvisioningOperationStatuses.Failed;
            operation.Error = "Operation cancelled.";
            operation.CanResume = true;
            Save(operation);
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                RollbackRegistrySnapshots(operation);
            }
            catch (Exception rollbackEx)
            {
                operation.Status = ProvisioningOperationStatuses.Failed;
                operation.Error = $"{ex.Message} Registry rollback also failed: {rollbackEx.Message}";
                operation.CanResume = false;
                Save(operation);
                throw;
            }

            operation.Status = ProvisioningOperationStatuses.Failed;
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

    private static void AddUnsupportedDesiredStateItems(
        ProfileManifest profile,
        List<DesiredStateDiffItem> items)
    {
        if (profile.DesiredState is null)
            return;

        AddUnsupportedSection(
            profile.DesiredState.WindowsSettings.Count,
            DesiredStateDomainCodes.WindowsSetting,
            "windowsSettings",
            items);

        AddUnsupportedSection(
            profile.DesiredState.Policies.Count,
            DesiredStateDomainCodes.Policy,
            "policies",
            items);

        AddUnsupportedSection(
            profile.DesiredState.Optimizations.Count,
            DesiredStateDomainCodes.Optimization,
            "optimizations",
            items);
    }

    private void AddRegistryDiffItems(
        ProfileManifest profile,
        List<DesiredStateDiffItem> items)
    {
        var registrySettings = profile.DesiredState?.RegistrySettings;
        if (registrySettings is null || registrySettings.Count == 0)
            return;

        foreach (var desired in registrySettings)
        {
            var observation = _registry.Observe(desired);
            var targetId = $"registry:{desired.Hive}:{desired.Key}:{desired.ValueName}";

            if (!string.IsNullOrWhiteSpace(observation.Error))
            {
                items.Add(new DesiredStateDiffItem(
                    DesiredStateDomainCodes.RegistrySetting,
                    targetId,
                    targetId,
                    ProvisioningStateCodes.Unknown,
                    ProvisioningActionCodes.Blocked,
                    null,
                    null,
                    desired.Value,
                    observation.Error));
                continue;
            }

            if (!observation.Exists)
            {
                items.Add(new DesiredStateDiffItem(
                    DesiredStateDomainCodes.RegistrySetting,
                    targetId,
                    targetId,
                    ProvisioningStateCodes.Missing,
                    ProvisioningActionCodes.Set,
                    null,
                    null,
                    desired.Value,
                    "Registry value is missing and will be created."));
                continue;
            }

            var matches =
                string.Equals(observation.Value, desired.Value, StringComparison.Ordinal) &&
                string.Equals(observation.ValueType, desired.ValueType, StringComparison.OrdinalIgnoreCase);

            items.Add(new DesiredStateDiffItem(
                DesiredStateDomainCodes.RegistrySetting,
                targetId,
                targetId,
                matches ? DesiredStateStateCodes.Compliant : DesiredStateStateCodes.Drifted,
                matches ? ProvisioningActionCodes.None : ProvisioningActionCodes.Set,
                observation.Value,
                observation.ValueType,
                desired.Value,
                matches
                    ? "Registry value matches the desired state."
                    : "Registry value differs from the desired state. Registry mutation is not enabled yet."));
        }
    }

    private void AddConditionDiffItems(
        ProfileManifest profile,
        List<DesiredStateDiffItem> items)
    {
        var conditions = profile.DesiredState?.Conditions;
        if (conditions is null || conditions.Count == 0)
            return;

        var baseline = _windows.GetBaseline();
        foreach (var evaluation in MachineConditionEvaluator.Evaluate(conditions, baseline))
        {
            items.Add(new DesiredStateDiffItem(
                DesiredStateDomainCodes.Condition,
                $"condition:{evaluation.Condition.Fact}:{evaluation.Condition.Operator}:{evaluation.Condition.Value}",
                evaluation.Condition.Fact,
                evaluation.IsSatisfied
                    ? "COMPLIANT"
                    : ProvisioningStateCodes.Unknown,
                evaluation.IsSatisfied
                    ? ProvisioningActionCodes.None
                    : ProvisioningActionCodes.Blocked,
                evaluation.IsKnown ? evaluation.Message : null,
                null,
                evaluation.Condition.Value,
                evaluation.Message));
        }
    }

    private static void AddUnsupportedSection(
        int count,
        string domain,
        string sectionName,
        List<DesiredStateDiffItem> items)
    {
        if (count == 0)
            return;

        items.Add(new DesiredStateDiffItem(
            domain,
            sectionName,
            sectionName,
            ProvisioningStateCodes.Unknown,
            ProvisioningActionCodes.Blocked,
            null,
            null,
            count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"Desired-state section '{sectionName}' contains {count} item(s), but observation and execution are not implemented yet."));
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

    private ProfileManifest GetProfile(string id) =>
        _config.LoadProfiles().TryGetValue(id, out var profile)
            ? profile
            : throw new InvalidOperationException($"Unknown profile: {id}");

    private static void ValidateUncompletedPlanState(
        ProvisioningOperation operation,
        ProvisioningPlan persistedPlan,
        ProvisioningPlan currentPlan)
    {
        if (persistedPlan.Items.Count != currentPlan.Items.Count)
        {
            operation.Status = ProvisioningOperationStatuses.Stale;
            operation.CanResume = false;
            operation.Error = "Provisioning plan is stale because the desired-state item set changed.";
            throw new InvalidOperationException(operation.Error);
        }

        for (var index = operation.Completed; index < persistedPlan.Items.Count; index++)
        {
            var expected = persistedPlan.Items[index];
            var observed = currentPlan.Items[index];

            if (!string.Equals(expected.Domain, observed.Domain, StringComparison.Ordinal)
                || !string.Equals(expected.TargetId, observed.TargetId, StringComparison.Ordinal)
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

    private static RegistrySettingDesiredState FindRegistryDesired(
        ProfileManifest profile,
        string targetId)
    {
        var desired = profile.DesiredState?.RegistrySettings
            .FirstOrDefault(item =>
                string.Equals(
                    $"registry:{item.Hive}:{item.Key}:{item.ValueName}",
                    targetId,
                    StringComparison.Ordinal));

        return desired
            ?? throw new InvalidOperationException($"Unknown registry desired-state target '{targetId}'.");
    }

    private static void ValidateRegistryPostcondition(
        ProvisioningPlanItem planned,
        ProvisioningPlanItem verified)
    {
        if (verified.StateCode != DesiredStateStateCodes.Compliant
            || verified.ActionCode != ProvisioningActionCodes.None)
        {
            throw new InvalidOperationException(
                $"Post-condition verification failed for registry target '{planned.ComponentName}': {verified.Message}");
        }
    }

    private void RollbackRegistrySnapshots(ProvisioningOperation operation)
    {
        var failures = new List<string>();

        var rollbackStartIndex = operation.RegistrySnapshots
            .Select(snapshot => snapshot.PlanIndex)
            .DefaultIfEmpty(operation.Completed)
            .Min();

        foreach (var snapshot in operation.RegistrySnapshots.AsEnumerable().Reverse())
        {
            try
            {
                _registryWriter.Restore(snapshot);
            }
            catch (Exception ex)
            {
                failures.Add($"{snapshot.Hive}\\{snapshot.Key}\\{snapshot.ValueName}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "Registry rollback failed: " + string.Join(" | ", failures));

        operation.Completed = Math.Min(operation.Completed, rollbackStartIndex);
        operation.RegistrySnapshots.Clear();
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
