using System.Text.Json;
using WorkspaceControl.Application;
using WorkspaceControl.Infrastructure;
using WorkspaceControl.Domain;

try
{
    var application = WorkspaceControlApplicationFactory.Create();
    var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
    var profile = GetOption(args, "--profile");
    var operation = GetOption(args, "--operation");
    object? data;
    IReadOnlyList<string> messages = Array.Empty<string>();

    switch (command)
    {
        case "capabilities":
            data = new[] { "baseline", "provisioning", "optimization", "official-sources", "winget-fallback" };
            break;
        case "baseline":
            data = application.GetBaseline();
            break;
        case "inventory":
            data = await application.GetInventoryAsync();
            break;
        case "optimization-plan-safe":
            data = application.GetSafeOptimizationPlan();
            break;
        case "optimization-apply-safe":
            messages = application.ApplySafeOptimization();
            data = new { Applied = messages.Count };
            break;
        case "optimization-rollback":
            messages = application.RollbackOptimization();
            data = new { Restored = messages.Count };
            break;
        case "provisioning-profiles":
            data = application.GetWorkspaces()
                .Select(x => new { x.Id, x.Name, x.Description })
                .ToArray();
            break;
        case "desired-state-diff":
            data = MapDiff(await application.GetDesiredStateDiffAsync(profile ?? throw new ArgumentException("--profile est requis.")));
            break;
        case "provisioning-plan":
            data = MapPlan(await application.GetPlanAsync(profile ?? throw new ArgumentException("--profile est requis.")));
            break;
        case "provisioning-worker":
            await application.RunProvisioningAsync(
                operation ?? throw new ArgumentException("--operation est requis."),
                CancellationToken.None);
            data = new { OperationId = operation, Status = "completed" };
            break;
        case "provisioning-start":
            data = MapOperation(await application.CreateProvisioningAsync(
                profile ?? throw new ArgumentException("--profile est requis.")));
            break;
        case "provisioning-confirm":
            data = new
            {
                OperationId = application.ConfirmProvisioning(
                    operation ?? throw new ArgumentException("--operation est requis.")),
                Status = "queued"
            };
            break;
        case "provisioning-status":
            data = MapOperation(application.GetProvisioningStatus(operation ?? throw new ArgumentException("--operation est requis.")));
            break;
        case "provisioning-recovery":
            data = MapOperation(application.GetProvisioningRecovery());
            break;
        case "provisioning-resume":
            data = new
            {
                OperationId = application.ResumeProvisioning(operation ?? throw new ArgumentException("--operation est requis.")),
                Status = "starting"
            };
            break;
        case "provisioning-history":
            data = application.GetProvisioningHistory().Select(MapHistory).ToArray();
            break;
        case "provisioning-detail":
            data = MapDetail(application.GetProvisioningDetail(operation ?? throw new ArgumentException("--operation est requis.")));
            break;
        default:
            throw new ArgumentException($"Commande inconnue : {command}");
    }

    WriteResponse(true, 0, data, messages, null);
    return 0;
}
catch (Exception ex)
{
    WriteResponse(false, 1, null, Array.Empty<string>(), ex.Message);
    return 1;
}

static object MapDiff(DesiredStateDiff diff)
{
    return new
    {
        Profile = new
        {
            Id = diff.WorkspaceId,
            Name = diff.WorkspaceName
        },
        InventoryScanId = diff.InventoryScanId,
        InventoryDiagnostics = diff.InventoryDiagnostics,
        Items = diff.Items.Select(item => new
        {
            Domain = item.Domain,
            Id = item.TargetId,
            Name = item.TargetName,
            StateCode = item.StateCode,
            ActionCode = item.ActionCode,
            ObservedValue = item.ObservedValue,
            AvailableValue = item.AvailableValue,
            DesiredValue = item.DesiredValue,
            Message = item.Message
        }).ToArray()
    };
}

static object MapPlan(ProvisioningPlan plan)
{
    return new
    {
        Profile = new
        {
            Id = plan.WorkspaceId,
            Name = plan.WorkspaceName
        },
        InventoryScanId = plan.InventoryScanId,
        InventoryDiagnostics = plan.InventoryDiagnostics,
        Items = plan.Items.Select(item => new
        {
            Id = item.ComponentId,
            TargetId = item.TargetId,
            Domain = item.Domain,
            Name = item.ComponentName,
            StateCode = item.StateCode,
            ActionCode = item.ActionCode,
            InstalledVersion = item.InstalledVersion,
            AvailableVersion = item.AvailableVersion,
            DesiredVersion = item.DesiredVersion,
            ObservedValue = item.ObservedValue,
            Message = item.Message
        }).ToArray()
    };
}

static object? MapOperation(ProvisioningOperation? operation)
{
    if (operation is null) return null;

    var percent = operation.Total == 0 ? 100 : operation.Completed * 100.0 / operation.Total;
    return new
    {
        SchemaVersion = 1,
        OperationId = operation.OperationId,
        Status = operation.Status,
        Phase = "provisioning",
        WorkspaceId = operation.WorkspaceId,
        CurrentComponentId = operation.Steps.LastOrDefault(x => x.Status == "completed")?.ComponentId,
        CurrentComponentName = operation.CurrentComponentName,
        Completed = operation.Completed,
        Total = operation.Total,
        Percent = percent,
        MessageKey = operation.Status switch
        {
            "awaiting-confirmation" => "operation.awaitingConfirmation",
            "queued" => "operation.queued",
            "running" => "component.running",
            "completed" => "operation.completed",
            "failed" => "operation.failed",
            "stale" => "operation.stale",
            _ => "operation.unknown"
        },
        Error = operation.Error,
        CanResume = operation.CanResume,
        NextIndex = operation.Completed,
        WorkerPid = (int?)null,
        RunId = operation.OperationId,
        UpdatedAt = operation.UpdatedAt
    };
}

static object? MapDetail(ProvisioningOperation? operation)
{
    if (operation is null) return null;
    return new
    {
        Operation = MapOperation(operation),
        RunId = operation.OperationId,
        Logs = operation.Steps.Select(x => $"{x.ComponentId}: {x.Status}{(string.IsNullOrWhiteSpace(x.Error) ? "" : $" — {x.Error}")}").ToArray(),
        Summary = new
        {
            RunId = operation.OperationId,
            Mode = "provisioning",
            Selection = operation.WorkspaceId,
            StartedAt = operation.UpdatedAt,
            FinishedAt = operation.Status is "completed" or "failed" ? operation.UpdatedAt : (DateTimeOffset?)null,
            ExitCode = operation.Status == "completed" ? 0 : 1,
            Success = operation.Status == "completed",
            Steps = operation.Steps.Select(x => new { Id = x.ComponentId, x.Status, ExitCode = x.Status == "completed" ? 0 : 1, x.Error }).ToArray()
        }
    };
}

static object MapHistory(ProvisioningOperation operation) =>
    new
    {
        SchemaVersion = 1,
        OperationId = operation.OperationId,
        Status = operation.Status,
        Phase = "provisioning",
        WorkspaceId = operation.WorkspaceId,
        Completed = operation.Completed,
        Total = operation.Total,
        Percent = operation.Total == 0 ? 100 : operation.Completed * 100.0 / operation.Total,
        MessageKey = operation.Status switch
        {
            "awaiting-confirmation" => "operation.awaitingConfirmation",
            "completed" => "operation.completed",
            "stale" => "operation.stale",
            _ => "operation.failed"
        },
        CanResume = operation.CanResume,
        UpdatedAt = operation.UpdatedAt
    };

static string? GetOption(string[] args, string name)
{
    var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static void WriteResponse(bool success, int exitCode, object? data, IReadOnlyList<string> messages, string? error) =>
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        command = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ?? "",
        success,
        exitCode,
        timestampUtc = DateTimeOffset.UtcNow,
        data,
        messages,
        error
    }, JsonDefaults.Options));
