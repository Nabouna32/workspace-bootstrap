using System.Text.Json;
using WorkspaceBootstrap;

try
{
    var engine = new EngineFacade();
    var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
    var profile = GetOption(args, "--profile");
    var operation = GetOption(args, "--operation");
    var cacheOnly = args.Any(x => x.Equals("--cache-only", StringComparison.OrdinalIgnoreCase));

    object? data;
    IReadOnlyList<string> messages = Array.Empty<string>();

    switch (command)
    {
        case "capabilities":
            data = new[] { "baseline", "provisioning", "optimization", "cache", "official-sources", "winget-fallback" };
            break;
        case "baseline":
            data = engine.GetBaseline();
            break;
        case "inventory":
            data = await engine.GetInventoryAsync();
            break;
        case "optimization-plan-safe":
            data = engine.GetSafeOptimizationPlan();
            break;
        case "optimization-apply-safe":
            messages = engine.ApplySafeOptimization();
            data = new { Applied = messages.Count };
            break;
        case "optimization-rollback":
            messages = engine.RollbackOptimization();
            data = new { Restored = messages.Count };
            break;
        case "provisioning-profiles":
            data = engine.GetProfiles()
                .Select(x => new { x.Id, x.Name, x.Description })
                .ToArray();
            break;
        case "provisioning-plan":
            data = MapPlan(await engine.GetPlanAsync(profile ?? throw new ArgumentException("--profile est requis.")));
            break;
        case "provisioning-worker":
            await engine.RunProvisioningAsync(
                operation ?? throw new ArgumentException("--operation est requis."),
                cacheOnly,
                CancellationToken.None);
            data = new { OperationId = operation, Status = "completed" };
            break;
        case "provisioning-start":
            data = new
            {
                OperationId = engine.StartProvisioning(profile ?? throw new ArgumentException("--profile est requis."), cacheOnly),
                Status = "starting"
            };
            break;
        case "provisioning-status":
            data = MapOperation(engine.GetProvisioningStatus(operation ?? throw new ArgumentException("--operation est requis.")));
            break;
        case "provisioning-recovery":
            data = MapOperation(engine.GetProvisioningRecovery());
            break;
        case "provisioning-resume":
            data = new
            {
                OperationId = engine.ResumeProvisioning(operation ?? throw new ArgumentException("--operation est requis."), cacheOnly),
                Status = "starting"
            };
            break;
        case "provisioning-history":
            data = engine.GetProvisioningHistory().Select(MapHistory).ToArray();
            break;
        case "provisioning-detail":
            data = MapDetail(engine.GetProvisioningDetail(operation ?? throw new ArgumentException("--operation est requis.")));
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

static object MapPlan(object plan)
{
    var json = JsonSerializer.SerializeToElement(plan);
    var profile = json.GetProperty("Profile");
    var items = json.GetProperty("Items").EnumerateArray()
        .Select(x => new
        {
            Id = x.GetProperty("Id").GetString() ?? "",
            Name = x.GetProperty("Name").GetString() ?? "",
            StateCode = x.GetProperty("StateCode").GetString() ?? "UNKNOWN",
            ActionCode = x.GetProperty("ActionCode").GetString() ?? "version-unverified",
            Message = x.GetProperty("Message").GetString() ?? "État inconnu."
        }).ToArray();

    var diagnostics = json.TryGetProperty("InventoryDiagnostics", out var diagnosticElement)
        ? diagnosticElement
        : JsonSerializer.SerializeToElement(Array.Empty<object>());

    return new
    {
        Profile = new
        {
            Id = profile.GetProperty("Id").GetString() ?? "",
            Name = profile.GetProperty("Name").GetString() ?? "",
            Description = profile.GetProperty("Description").GetString() ?? ""
        },
        InventoryScanId = json.TryGetProperty("InventoryScanId", out var scanId)
            ? scanId.GetString()
            : null,
        InventoryDiagnostics = diagnostics,
        Items = items
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
        ProfileId = operation.ProfileId,
        CurrentComponentId = operation.Steps.LastOrDefault(x => x.Status == "completed")?.ComponentId,
        CurrentComponentName = operation.CurrentComponentName,
        Completed = operation.Completed,
        Total = operation.Total,
        Percent = percent,
        MessageKey = operation.Status switch
        {
            "starting" => "operation.starting",
            "running" => "component.running",
            "completed" => "operation.completed",
            "failed" => "operation.failed",
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
            Selection = operation.ProfileId,
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
        ProfileId = operation.ProfileId,
        Completed = operation.Completed,
        Total = operation.Total,
        Percent = operation.Total == 0 ? 100 : operation.Completed * 100.0 / operation.Total,
        MessageKey = operation.Status == "completed" ? "operation.completed" : "operation.failed",
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
