using System.Diagnostics;
using System.Text.Json;

namespace WorkspaceControl.Infrastructure;

public sealed class WorkspaceOperationEngine
{
    private readonly ConfigurationStore _config;
    private readonly InstallerEngine _installer;
    private readonly WorkspacePaths _paths;
    private readonly InventoryScanner _inventory;
    private readonly WindowsSystemService _windows;
    private readonly RegistryDesiredStateObserver _registry;
    private readonly RegistryDesiredStateWriter _registryWriter;

    public WorkspaceOperationEngine(
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

    public IReadOnlyList<WorkspaceManifest> Workspaces() =>
        _config.LoadWorkspaces().Values.OrderBy(x => x.Name).ToArray();

    public IReadOnlyList<ComponentManifest> ApplicationCatalog() =>
        _config.LoadComponents().Values.OrderBy(x => x.Name).ToArray();

    public WorkspaceManifest CreateWorkspace(
        string name,
        string description,
        IReadOnlyCollection<string> componentIds) =>
        _config.CreateWorkspace(name, description, componentIds);

    public void SaveWorkspace(WorkspaceManifest workspace) =>
        _config.SaveWorkspace(workspace);

    public async Task<DesiredStateDiff> DiffAsync(
        string workspaceId,
        CancellationToken token = default)
    {
        var workspace = GetWorkspace(workspaceId);
        var components = _config.LoadComponents();
        var inventory = await _inventory.ScanAsync(token);

        var requests = workspace.ApplicationRequests;

        var items = requests.Select(request =>
        {
            if (!components.TryGetValue(request.ComponentId, out var catalogComponent))
                throw new InvalidOperationException($"Unknown component: {request.ComponentId}");

            var component = ApplyWorkspaceOverrides(catalogComponent, request);
            var match = FindInventoryMatch(component, inventory.Items);
            var planItem = BuildApplicationPlanItem(
                component,
                request,
                match,
                inventory.ProviderDiagnostics);

            return new DesiredStateDiffItem(
                DesiredStateDomainCodes.Application,
                planItem.ComponentId,
                planItem.ComponentName,
                planItem.StateCode,
                planItem.ActionCode,
                planItem.InstalledVersion,
                planItem.AvailableVersion,
                planItem.DesiredVersion,
                planItem.Message,
                planItem.RemovalCapability);
        }).ToList();

        AddUnsupportedDesiredStateItems(workspace, items);
        AddConditionDiffItems(workspace, items);
        AddRegistryDiffItems(workspace, items);

        return new DesiredStateDiff(
            workspace.Id,
            workspace.Name,
            inventory.ScanId,
            inventory.ProviderDiagnostics,
            items,
            DateTimeOffset.UtcNow);
    }

    public async Task<WorkspacePlan> PlanAsync(
        string workspaceId,
        CancellationToken token = default)
    {
        var diff = await DiffAsync(workspaceId, token);

        var unsupported = diff.Items
            .Where(item =>
                item.Domain != DesiredStateDomainCodes.Application &&
                item.ActionCode == DesiredStateActionCodes.Blocked)
            .ToArray();

        if (unsupported.Length > 0)
        {
            throw new InvalidOperationException(
                $"Workspace '{diff.WorkspaceId}' contains desired-state sections that are not executable yet. " +
                "The diff identifies these sections explicitly; no mutation plan was created.");
        }

        var items = diff.Items
            .Where(item => item.Domain != DesiredStateDomainCodes.Condition)
            .Select(item => new WorkspacePlanItem(
                item.TargetId,
                item.TargetName,
                item.StateCode,
                item.ActionCode,
                item.ObservedValue,
                item.AvailableValue,
                item.DesiredValue,
                item.Message,
                item.Domain,
                item.TargetId,
                item.ObservedValue,
                item.RemovalCapability))
            .ToArray();

        return new WorkspacePlan(
            diff.WorkspaceId,
            diff.WorkspaceName,
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

    private static WorkspacePlanItem BuildApplicationPlanItem(
        ComponentManifest component,
        WorkspaceApplication request,
        InventoryItem? match,
        IReadOnlyList<InventoryProviderDiagnostic> diagnostics)
    {
        if (!string.Equals(request.State, "present", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.State, "absent", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkspacePlanItem(
                component.Id,
                component.Name,
                ApplicationStateCodes.Unknown,
                DesiredStateActionCodes.Blocked,
                match?.Version,
                null,
                null,
                $"Unsupported application desired-state '{request.State}'.");
        }

        var desiredPresent = string.Equals(
            request.State,
            "present",
            StringComparison.OrdinalIgnoreCase);

        if (!desiredPresent)
        {
            if (diagnostics.Any(diagnostic => !diagnostic.Success))
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Unknown,
                    DesiredStateActionCodes.Blocked,
                    match?.Version,
                    null,
                    null,
                    "Inventory is incomplete; Workspace Control cannot safely verify application removal.");
            }

            if (match is null)
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Absent,
                    DesiredStateActionCodes.None,
                    null,
                    null,
                    null,
                    "Application is absent as requested.");
            }

            if (string.IsNullOrWhiteSpace(match.Version))
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Unknown,
                    DesiredStateActionCodes.Blocked,
                    null,
                    null,
                    null,
                    "Installed application version is unknown; safe rollback cannot be guaranteed.");
            }

            if (string.IsNullOrWhiteSpace(component.PackageId))
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Unknown,
                    DesiredStateActionCodes.Blocked,
                    match.Version,
                    null,
                    null,
                    "Application does not declare a package id for the supported uninstall provider.");
            }

            var removalCapability = match.RemovalCapabilities
                .FirstOrDefault(capability =>
                    string.Equals(
                        capability.Kind,
                        RemovalCapabilityKindCodes.WinGet,
                        StringComparison.OrdinalIgnoreCase));

            if (removalCapability is null)
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Installed,
                    DesiredStateActionCodes.Blocked,
                    match.Version,
                    null,
                    null,
                    "Application is installed, but no supported removal capability was observed.");
            }

            return new WorkspacePlanItem(
                component.Id,
                component.Name,
                ApplicationStateCodes.Installed,
                DesiredStateActionCodes.Remove,
                match.Version,
                null,
                null,
                $"Application version {match.Version} is installed but absent from the desired state and will be removed.",
                RemovalCapability: removalCapability);
        }

        if (match is null)
        {
            if (diagnostics.Any(diagnostic => !diagnostic.Success))
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Unknown,
                    DesiredStateActionCodes.Blocked,
                    null,
                    null,
                    null,
                    "Inventory is incomplete; Workspace Control cannot safely determine whether the component is installed.");
            }

            return new WorkspacePlanItem(
                component.Id,
                component.Name,
                ApplicationStateCodes.Missing,
                DesiredStateActionCodes.Install,
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
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Unknown,
                    DesiredStateActionCodes.Blocked,
                    installedVersion,
                    availableVersion,
                    component.MinimumVersion,
                    "The component declares a minimum-version policy without a valid minimum version.");
            }

            if (!TryParseVersion(installedVersion, out var installed)
                || installed < minimumVersion)
            {
                return new WorkspacePlanItem(
                    component.Id,
                    component.Name,
                    ApplicationStateCodes.Outdated,
                    DesiredStateActionCodes.Update,
                    installedVersion,
                    availableVersion,
                    component.MinimumVersion,
                    $"Installed version: {installedVersion ?? "unknown"}; minimum required version: {component.MinimumVersion}.");
            }

            return new WorkspacePlanItem(
                component.Id,
                component.Name,
                ApplicationStateCodes.Installed,
                DesiredStateActionCodes.None,
                installedVersion,
                availableVersion,
                component.MinimumVersion,
                $"Installed version: {installedVersion}; minimum required version: {component.MinimumVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(policy) && policy != "latest-stable" && policy != "stable-compatible")
        {
            return new WorkspacePlanItem(
                component.Id,
                component.Name,
                ApplicationStateCodes.Unknown,
                DesiredStateActionCodes.Blocked,
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

            return new WorkspacePlanItem(
                component.Id,
                component.Name,
                ApplicationStateCodes.Outdated,
                DesiredStateActionCodes.Update,
                installedVersion,
                availableVersion,
                availableVersion,
                $"Installed version: {installedVersion ?? "unknown"}; {policyDescription} version: {availableVersion}.");
        }

        return new WorkspacePlanItem(
            component.Id,
            component.Name,
            ApplicationStateCodes.Installed,
            DesiredStateActionCodes.None,
            installedVersion,
            null,
            null,
            $"Installed version: {installedVersion ?? "unknown"}.");
    }

    private static void ValidatePostcondition(
        ComponentManifest component,
        WorkspacePlanItem planned,
        WorkspacePlanItem verified)
    {
        if (verified.StateCode != ApplicationStateCodes.Installed
            || verified.ActionCode != DesiredStateActionCodes.None)
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

    private static void ValidateRemovePostcondition(
        WorkspacePlanItem planned,
        WorkspacePlanItem verified)
    {
        if (verified.StateCode != ApplicationStateCodes.Absent
            || verified.ActionCode != DesiredStateActionCodes.None)
        {
            throw new InvalidOperationException(
                $"Post-condition verification failed for '{planned.ComponentName}': {verified.Message}");
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

    public async Task<WorkspaceOperation> CreateAsync(
        string workspaceId,
        CancellationToken token = default)
    {
        var plan = await PlanAsync(workspaceId, token);
        var operation = new WorkspaceOperation
        {
            WorkspaceId = plan.WorkspaceId,
            Plan = plan,
            Status = WorkspaceOperationStatuses.AwaitingConfirmation,
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

        if (operation.Status != WorkspaceOperationStatuses.AwaitingConfirmation)
            throw new InvalidOperationException(
                "Only an operation awaiting confirmation can be confirmed.");

        if (operation.Plan is null)
            throw new InvalidOperationException(
                "The operation does not contain a workspace operation plan.");

        if (operation.Plan.Items.Any(item => item.ActionCode == DesiredStateActionCodes.Blocked))
            throw new InvalidOperationException(
                "The workspace operation plan contains blocked actions and cannot be confirmed.");

        operation.Status = WorkspaceOperationStatuses.Queued;
        operation.CanResume = false;
        operation.Error = null;
        Save(operation);

        try
        {
            LaunchWorker(operation.OperationId);
        }
        catch (Exception ex)
        {
            operation.Status = WorkspaceOperationStatuses.Failed;
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

        if (operation.Status is WorkspaceOperationStatuses.Completed)
            return;

        await using var operationLock = await AcquireOperationLockAsync(token);

        operation = Get(operationId)
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status is WorkspaceOperationStatuses.Completed)
            return;

        var workspace = GetWorkspace(operation.WorkspaceId);
        var components = _config.LoadComponents();
        var plan = operation.Plan
            ?? throw new InvalidOperationException(
                "The operation has no persisted workspace operation plan.");

        if (operation.Status is WorkspaceOperationStatuses.AwaitingConfirmation
            or WorkspaceOperationStatuses.Stale
            or WorkspaceOperationStatuses.Completed)
            throw new InvalidOperationException(
                "The workspace operation is not in an executable state.");

        try
        {
            var currentPlan = await PlanAsync(operation.WorkspaceId, token);
            ValidateUncompletedPlanState(operation, plan, currentPlan);

            operation.Status = WorkspaceOperationStatuses.Running;
            operation.Error = null;
            operation.CanResume = true;
            Save(operation);

            for (var index = operation.Completed; index < plan.Items.Count; index++)
            {
                token.ThrowIfCancellationRequested();

                var planned = plan.Items[index];
                operation.CurrentComponentName = planned.ComponentName;
                Save(operation);

                if (planned.ActionCode == DesiredStateActionCodes.Blocked)
                    throw new InvalidOperationException(planned.Message);

                if (planned.ActionCode == DesiredStateActionCodes.None)
                {
                    operation.Steps.Add(
                        new WorkspaceOperationStep(
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
                        var desired = FindRegistryDesired(workspace, planned.TargetId ?? planned.ComponentId);
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

                        var request = workspace.ApplicationRequests
                            .FirstOrDefault(item =>
                                string.Equals(item.ComponentId, planned.ComponentId, StringComparison.Ordinal))
                            ?? throw new InvalidOperationException(
                                $"Workspace '{workspace.Id}' does not contain application '{planned.ComponentId}'.");

                        var component = ApplyWorkspaceOverrides(catalogComponent, request);

                        if (planned.ActionCode == DesiredStateActionCodes.Remove)
                        {
                            operation.ApplicationSnapshots.Add(
                                new WorkspaceApplicationSnapshot(
                                    planned.ComponentId,
                                    planned.InstalledVersion,
                                    planned.RemovalCapability,
                                    index));
                            Save(operation);

                            if (planned.RemovalCapability is null)
                                throw new InvalidOperationException(
                                    $"No supported removal capability is persisted for '{planned.ComponentName}'.");

                            await _installer.UninstallAsync(
                                component,
                                planned.RemovalCapability,
                                planned.InstalledVersion,
                                token);
                        }
                        else
                        {
                            await _installer.InstallAsync(
                                component,
                                planned.ActionCode,
                                string.Equals(component.VersionPolicy, "minimum", StringComparison.OrdinalIgnoreCase)
                                    ? null
                                    : planned.DesiredVersion,
                                token);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Unsupported executable desired-state domain '{planned.Domain}'.");
                    }

                    operation.CurrentComponentName = $"Verifying: {planned.ComponentName}";
                    Save(operation);

                    var verificationPlan = await PlanAsync(operation.WorkspaceId, token);
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
                        if (planned.ActionCode == DesiredStateActionCodes.Remove)
                        {
                            ValidateRemovePostcondition(planned, verified);
                        }
                        else
                        {
                            if (!components.TryGetValue(planned.ComponentId, out var catalogComponent))
                                throw new InvalidOperationException($"Unknown component: {planned.ComponentId}");

                            var request = workspace.ApplicationRequests
                                .FirstOrDefault(item =>
                                    string.Equals(item.ComponentId, planned.ComponentId, StringComparison.Ordinal))
                                ?? throw new InvalidOperationException(
                                    $"Workspace '{workspace.Id}' does not contain application '{planned.ComponentId}'.");

                            ValidatePostcondition(
                                ApplyWorkspaceOverrides(catalogComponent, request),
                                planned,
                                verified);
                        }
                    }

                    operation.Steps.Add(
                        new WorkspaceOperationStep(
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
                        new WorkspaceOperationStep(
                            planned.TargetId ?? planned.ComponentId,
                            planned.ComponentName,
                            "failed",
                            ex.Message));

                    try
                    {
                        await RollbackSnapshotsAsync(operation, components, token);
                    }
                    catch (Exception rollbackEx)
                    {
                        operation.Error = $"{ex.Message} Rollback also failed: {rollbackEx.Message}";
                        operation.Status = WorkspaceOperationStatuses.Failed;
                        operation.CanResume = false;
                        Save(operation);
                        return;
                    }

                    operation.Error = ex.Message;
                    operation.Status = WorkspaceOperationStatuses.Failed;
                    operation.CanResume = true;
                    Save(operation);
                    return;
                }
            }

            operation.CurrentComponentName = "Verifying final state…";
            Save(operation);

            var finalPlan = await PlanAsync(operation.WorkspaceId, token);
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

                if (planned.ActionCode == DesiredStateActionCodes.None)
                {
                    if (planned.StateCode == ApplicationStateCodes.Absent)
                    {
                        ValidateRemovePostcondition(planned, verified);
                    }
                    else if (verified.StateCode != ApplicationStateCodes.Installed
                             || verified.ActionCode != DesiredStateActionCodes.None)
                    {
                        throw new InvalidOperationException(
                            $"Final state verification failed for '{planned.ComponentName}': {verified.Message}");
                    }

                    continue;
                }

                if (planned.ActionCode == DesiredStateActionCodes.Remove)
                {
                    ValidateRemovePostcondition(planned, verified);
                    continue;
                }

                if (!components.TryGetValue(planned.ComponentId, out var catalogComponent))
                    throw new InvalidOperationException($"Unknown component: {planned.ComponentId}");

                var request = workspace.ApplicationRequests
                    .FirstOrDefault(item =>
                        string.Equals(item.ComponentId, planned.ComponentId, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        $"Workspace '{workspace.Id}' does not contain application '{planned.ComponentId}'.");

                ValidatePostcondition(
                    ApplyWorkspaceOverrides(catalogComponent, request),
                    planned,
                    verified);
            }

            operation.Status = WorkspaceOperationStatuses.Completed;
            operation.CurrentComponentName = null;
            operation.CanResume = false;
            operation.Error = null;
            operation.RegistrySnapshots.Clear();
            operation.ApplicationSnapshots.Clear();
            Save(operation);
        }
        catch (OperationCanceledException)
        {
            try
            {
                await RollbackSnapshotsAsync(
                    operation,
                    components,
                    CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                operation.Error = $"Operation cancelled. Rollback failed: {rollbackEx.Message}";
                operation.Status = WorkspaceOperationStatuses.Failed;
                operation.CanResume = false;
                Save(operation);
                throw;
            }

            operation.Status = WorkspaceOperationStatuses.Failed;
            operation.Error = "Operation cancelled and completed changes were rolled back.";
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
                operation.Status = WorkspaceOperationStatuses.Failed;
                operation.Error = $"{ex.Message} Rollback also failed: {rollbackEx.Message}";
                operation.CanResume = false;
                Save(operation);
                throw;
            }

            if (operation.Status == WorkspaceOperationStatuses.Stale)
            {
                Save(operation);
                throw;
            }

            operation.Status = WorkspaceOperationStatuses.Failed;
            operation.Error = ex.Message;
            operation.CanResume = true;
            Save(operation);
            throw;
        }
    }

    public WorkspaceOperation? Get(string id)
    {
        var path = OperationPath(id);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<WorkspaceOperation>(
                File.ReadAllText(path),
                JsonDefaults.Options)
            : null;
    }

    public IEnumerable<WorkspaceOperation> History() =>
        Directory.EnumerateFiles(_paths.StateRoot, "operation-*.json")
            .Select(path => JsonSerializer.Deserialize<WorkspaceOperation>(
                File.ReadAllText(path),
                JsonDefaults.Options))
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.UpdatedAt);

    public string Resume(string id)
    {
        var operation = Get(id)
            ?? throw new InvalidOperationException("Operation not found.");

        if (operation.Status != WorkspaceOperationStatuses.Failed || !operation.CanResume)
            throw new InvalidOperationException(
                "This operation cannot be resumed.");

        operation.Status = WorkspaceOperationStatuses.Queued;
        operation.CanResume = false;
        Save(operation);

        try
        {
            LaunchWorker(operation.OperationId);
        }
        catch (Exception ex)
        {
            operation.Status = WorkspaceOperationStatuses.Failed;
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
            psi.ArgumentList.Add("workspace-operation-worker");
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
            psi.ArgumentList.Add("workspace-operation-worker");
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
        var path = Path.Combine(_paths.StateRoot, "workspace-operation.lock");

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
        WorkspaceManifest workspace,
        List<DesiredStateDiffItem> items)
    {
        if (workspace.DesiredState is null)
            return;

        AddUnsupportedSection(
            workspace.DesiredState.WindowsSettings.Count,
            DesiredStateDomainCodes.WindowsSetting,
            "windowsSettings",
            items);

        AddUnsupportedSection(
            workspace.DesiredState.Policies.Count,
            DesiredStateDomainCodes.Policy,
            "policies",
            items);

        AddUnsupportedSection(
            workspace.DesiredState.Optimizations.Count,
            DesiredStateDomainCodes.Optimization,
            "optimizations",
            items);
    }

    private void AddRegistryDiffItems(
        WorkspaceManifest workspace,
        List<DesiredStateDiffItem> items)
    {
        var registrySettings = workspace.DesiredState?.RegistrySettings;
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
                    ApplicationStateCodes.Unknown,
                    DesiredStateActionCodes.Blocked,
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
                    ApplicationStateCodes.Missing,
                    DesiredStateActionCodes.Set,
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
                matches ? DesiredStateActionCodes.None : DesiredStateActionCodes.Set,
                observation.Value,
                observation.ValueType,
                desired.Value,
                matches
                    ? "Registry value matches the desired state."
                    : "Registry value differs from the desired state and will be updated after explicit confirmation."));
        }
    }

    private void AddConditionDiffItems(
        WorkspaceManifest workspace,
        List<DesiredStateDiffItem> items)
    {
        var conditions = workspace.DesiredState?.Conditions;
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
                    : ApplicationStateCodes.Unknown,
                evaluation.IsSatisfied
                    ? DesiredStateActionCodes.None
                    : DesiredStateActionCodes.Blocked,
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
            ApplicationStateCodes.Unknown,
            DesiredStateActionCodes.Blocked,
            null,
            null,
            count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            $"Desired-state section '{sectionName}' contains {count} item(s), but observation and execution are not implemented yet."));
    }

    private static ComponentManifest ApplyWorkspaceOverrides(
        ComponentManifest component,
        WorkspaceApplication request)
    {
        if (request.VersionPolicy is null && request.MinimumVersion is null)
            return component;

        return component with
        {
            VersionPolicy = request.VersionPolicy ?? component.VersionPolicy,
            MinimumVersion = request.MinimumVersion ?? component.MinimumVersion
        };
    }

    private WorkspaceManifest GetWorkspace(string id) =>
        _config.LoadWorkspaces().TryGetValue(id, out var workspace)
            ? workspace
            : throw new InvalidOperationException($"Unknown workspace: {id}");

    private static void ValidateUncompletedPlanState(
        WorkspaceOperation operation,
        WorkspacePlan persistedPlan,
        WorkspacePlan currentPlan)
    {
        if (persistedPlan.Items.Count != currentPlan.Items.Count)
        {
            operation.Status = WorkspaceOperationStatuses.Stale;
            operation.CanResume = false;
            operation.Error = "Workspace operation plan is stale because the desired-state item set changed.";
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
                || !string.Equals(expected.DesiredVersion, observed.DesiredVersion, StringComparison.Ordinal)
                || !string.Equals(expected.ObservedValue, observed.ObservedValue, StringComparison.Ordinal))
            {
                operation.Status = WorkspaceOperationStatuses.Stale;
                operation.CanResume = false;
                operation.Error =
                    $"Workspace operation plan is stale for '{expected.ComponentName}'. " +
                    $"Observed state is {observed.StateCode}/{observed.ActionCode} " +
                    $"but the confirmed plan expected {expected.StateCode}/{expected.ActionCode}.";
                throw new InvalidOperationException(operation.Error);
            }
        }
    }

    private static RegistrySettingDesiredState FindRegistryDesired(
        WorkspaceManifest workspace,
        string targetId)
    {
        var desired = workspace.DesiredState?.RegistrySettings
            .FirstOrDefault(item =>
                string.Equals(
                    $"registry:{item.Hive}:{item.Key}:{item.ValueName}",
                    targetId,
                    StringComparison.Ordinal));

        return desired
            ?? throw new InvalidOperationException($"Unknown registry desired-state target '{targetId}'.");
    }

    private static void ValidateRegistryPostcondition(
        WorkspacePlanItem planned,
        WorkspacePlanItem verified)
    {
        if (verified.StateCode != DesiredStateStateCodes.Compliant
            || verified.ActionCode != DesiredStateActionCodes.None)
        {
            throw new InvalidOperationException(
                $"Post-condition verification failed for registry target '{planned.ComponentName}': {verified.Message}");
        }
    }

    private async Task RollbackSnapshotsAsync(
        WorkspaceOperation operation,
        IReadOnlyDictionary<string, ComponentManifest> components,
        CancellationToken token)
    {
        var failures = new List<string>();

        try
        {
            await RollbackApplicationSnapshotsAsync(operation, components, CancellationToken.None);
        }
        catch (Exception ex)
        {
            failures.Add(ex.Message);
        }

        try
        {
            RollbackRegistrySnapshots(operation);
        }
        catch (Exception ex)
        {
            failures.Add(ex.Message);
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" | ", failures));
    }

    private async Task RollbackApplicationSnapshotsAsync(
        WorkspaceOperation operation,
        IReadOnlyDictionary<string, ComponentManifest> components,
        CancellationToken token)
    {
        var failures = new List<string>();

        var rollbackStartIndex = operation.ApplicationSnapshots
            .Select(snapshot => snapshot.PlanIndex)
            .DefaultIfEmpty(operation.Completed)
            .Min();

        foreach (var snapshot in operation.ApplicationSnapshots.AsEnumerable().Reverse())
        {
            try
            {
                if (string.IsNullOrWhiteSpace(snapshot.InstalledVersion))
                    throw new InvalidOperationException("The installed version required for rollback is unavailable.");

                if (!components.TryGetValue(snapshot.ComponentId, out var component))
                    throw new InvalidOperationException($"Unknown component '{snapshot.ComponentId}' during rollback.");

                if (snapshot.RemovalCapability is not null)
                {
                    await _installer.InstallFromRemovalCapabilityAsync(
                        snapshot.RemovalCapability,
                        component.Name,
                        snapshot.InstalledVersion,
                        token);
                }
                else
                {
                    await _installer.InstallAsync(
                        component,
                        DesiredStateActionCodes.Install,
                        snapshot.InstalledVersion,
                        token);
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{snapshot.ComponentId}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "Application rollback failed: " + string.Join(" | ", failures));

        var verificationPlan = await PlanAsync(operation.WorkspaceId, token);
        var verificationFailures = new List<string>();

        foreach (var snapshot in operation.ApplicationSnapshots)
        {
            var verified = verificationPlan.Items.FirstOrDefault(item =>
                string.Equals(item.Domain, DesiredStateDomainCodes.Application, StringComparison.Ordinal)
                && string.Equals(item.ComponentId, snapshot.ComponentId, StringComparison.Ordinal));

            if (verified is null
                || verified.StateCode != ApplicationStateCodes.Installed
                || verified.ActionCode != DesiredStateActionCodes.None
                || !string.Equals(
                    verified.InstalledVersion,
                    snapshot.InstalledVersion,
                    StringComparison.Ordinal))
            {
                verificationFailures.Add(
                    $"{snapshot.ComponentId}: expected version '{snapshot.InstalledVersion}', " +
                    $"observed '{verified?.InstalledVersion ?? "absent"}' with state/action " +
                    $"{verified?.StateCode ?? "unknown"}/{verified?.ActionCode ?? "unknown"}");
            }
        }

        if (verificationFailures.Count > 0)
            throw new InvalidOperationException(
                "Application rollback verification failed: " +
                string.Join(" | ", verificationFailures));

        operation.Completed = Math.Min(operation.Completed, rollbackStartIndex);
        operation.ApplicationSnapshots.Clear();
    }

    private void RollbackRegistrySnapshots(WorkspaceOperation operation)
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

    private void Save(WorkspaceOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.OperationId))
            throw new InvalidOperationException("Operation must have an identifier.");
        if (string.IsNullOrWhiteSpace(operation.WorkspaceId))
            throw new InvalidOperationException("Operation must reference a workspace.");
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
