using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceControl.Infrastructure;
using WorkspaceControl.Application;
using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Tests;

[TestClass]
public sealed class WorkspaceControlApplicationTests
{
    [TestMethod]
    public async Task Inventory_is_delegated_to_inventory_service()
    {
        var expected = new InventorySnapshot(
            "scan-1",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            [],
            []);

        var application = CreateApplication(inventory: new FakeInventoryService(expected));

        var actual = await application.GetInventoryAsync();

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task Provisioning_creation_is_delegated_without_applying()
    {
        var plan = new WorkspacePlan(
            "base",
            "Base",
            "scan-1",
            [],
            [],
            DateTimeOffset.UtcNow);
        var operation = new WorkspaceOperation
        {
            OperationId = "op-1",
            WorkspaceId = "base",
            Plan = plan,
            Status = WorkspaceOperationStatuses.AwaitingConfirmation
        };
        var provisioning = new FakeProvisioningService(operation);
        var application = CreateApplication(provisioning: provisioning);

        var actual = await application.CreateWorkspaceOperationAsync("base");

        Assert.AreSame(operation, actual);
        Assert.AreEqual(WorkspaceOperationStatuses.AwaitingConfirmation, actual.Status);
        Assert.AreSame(plan, actual.Plan);
        Assert.IsFalse(provisioning.Confirmed);
    }

    [TestMethod]
    public void Provisioning_confirmation_is_explicit()
    {
        var provisioning = new FakeProvisioningService();
        var application = CreateApplication(provisioning: provisioning);

        var operationId = application.ConfirmWorkspaceOperation("op-1");

        Assert.AreEqual("op-1", operationId);
        Assert.IsTrue(provisioning.Confirmed);
    }

    [TestMethod]
    public void Recovery_only_returns_the_latest_operation_when_recoverable()
    {
        var latestCompleted = new WorkspaceOperation
        {
            OperationId = "completed",
            Status = "completed",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var olderFailed = new WorkspaceOperation
        {
            OperationId = "failed",
            Status = "failed",
            UpdatedAt = latestCompleted.UpdatedAt.AddMinutes(-1)
        };

        var application = CreateApplication(
            provisioning: new FakeProvisioningService(latestCompleted, olderFailed));

        Assert.IsNull(application.GetRecoverableWorkspaceOperation());
    }

    [TestMethod]
    public void Recovery_does_not_offer_stale_operation()
    {
        var stale = new WorkspaceOperation
        {
            OperationId = "stale",
            Status = WorkspaceOperationStatuses.Stale,
            CanResume = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var application = CreateApplication(
            provisioning: new FakeProvisioningService(stale));

        Assert.IsNull(application.GetRecoverableWorkspaceOperation());
    }

    [TestMethod]
    public void Recovery_returns_latest_failed_operation()
    {
        var olderRunning = new WorkspaceOperation
        {
            OperationId = "running",
            Status = "running",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var latestFailed = new WorkspaceOperation
        {
            OperationId = "failed",
            Status = "failed",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var application = CreateApplication(
            provisioning: new FakeProvisioningService(latestFailed, olderRunning));

        Assert.AreSame(latestFailed, application.GetRecoverableWorkspaceOperation());
    }

    private static WorkspaceControlApplication CreateApplication(
        IWorkspaceOperationService? provisioning = null,
        IInventoryService? inventory = null)
    {
        return new WorkspaceControlApplication(
            provisioning ?? new FakeProvisioningService(),
            inventory ?? new FakeInventoryService(),
            new FakeSoftwareInventoryService(),
            new FakeWindowsAdministrationService(),
            new FakeOptimizationService());
    }

    private sealed class FakeInventoryService(InventorySnapshot snapshot) : IInventoryService
    {
        public FakeInventoryService()
            : this(new InventorySnapshot("empty", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []))
        {
        }

        public Task<InventorySnapshot> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class FakeProvisioningService(params WorkspaceOperation[] history) : IWorkspaceOperationService
    {
        public IReadOnlyList<WorkspaceManifest> GetWorkspaces() => [];
        public IReadOnlyList<ComponentManifest> GetApplicationCatalog() => [];
        public WorkspaceManifest CreateWorkspace(string name, string description, IReadOnlyCollection<string> componentIds) =>
            new("workspace-test", name, description, SchemaVersion: 2, DesiredState: new DesiredStateManifest(
                componentIds.Select(id => new WorkspaceApplication(id)).ToArray(), [], [], [], [], []));
        public void SaveWorkspace(WorkspaceManifest workspace) { }
        public Task<DesiredStateDiff> GetDesiredStateDiffAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DesiredStateDiff(
                workspaceId,
                workspaceId,
                "test-scan",
                [],
                [],
                DateTimeOffset.UtcNow));

        public Task<WorkspacePlan> CreateWorkspacePlanAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkspacePlan(
                workspaceId,
                workspaceId,
                "test-scan",
                [],
                [],
                DateTimeOffset.UtcNow));
        public bool Confirmed { get; private set; }

        public Task<WorkspaceOperation> CreateAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(history.FirstOrDefault() ?? new WorkspaceOperation
            {
                OperationId = "operation",
                WorkspaceId = workspaceId,
                Plan = new WorkspacePlan(workspaceId, workspaceId, "test-scan", [], [], DateTimeOffset.UtcNow),
                Status = WorkspaceOperationStatuses.AwaitingConfirmation
            });

        public string Confirm(string operationId)
        {
            Confirmed = true;
            return operationId;
        }

        public Task RunAsync(string operationId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public WorkspaceOperation? Get(string operationId) =>
            history.FirstOrDefault(x => x.OperationId == operationId);
        public string Resume(string operationId) => operationId;
        public IReadOnlyList<WorkspaceOperation> GetHistory() => history;
    }

    private sealed class FakeSoftwareInventoryService : ISoftwareInventoryService
    {
        public Task<SoftwareInventorySnapshot> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SoftwareInventorySnapshot("software", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []));
    }

    private sealed class FakeWindowsAdministrationService : IWindowsAdministrationService
    {
        public BaselineSnapshot GetBaseline() =>
            new("Windows", "build", "x64", "CPU", 1, 1, 1, [], []);
    }

    private sealed class FakeOptimizationService : IOptimizationService
    {
        public IReadOnlyList<OptimizationPlanItem> GetSafePlan() => [];
        public IReadOnlyList<string> ApplySafe() => [];
        public IReadOnlyList<string> Rollback() => [];
    }
}
