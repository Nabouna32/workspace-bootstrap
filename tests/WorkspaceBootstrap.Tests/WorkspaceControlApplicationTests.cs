using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceControl.Infrastructure;
using WorkspaceControl.Application;
using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceBootstrap.Tests;

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
        var plan = new ProvisioningPlan(
            "base",
            "Base",
            "scan-1",
            [],
            [],
            DateTimeOffset.UtcNow);
        var operation = new ProvisioningOperation
        {
            OperationId = "op-1",
            ProfileId = "base",
            Plan = plan,
            Status = ProvisioningOperationStatuses.AwaitingConfirmation
        };
        var provisioning = new FakeProvisioningService(operation);
        var application = CreateApplication(provisioning: provisioning);

        var actual = await application.CreateProvisioningAsync("base");

        Assert.AreSame(operation, actual);
        Assert.AreEqual(ProvisioningOperationStatuses.AwaitingConfirmation, actual.Status);
        Assert.AreSame(plan, actual.Plan);
        Assert.IsFalse(provisioning.Confirmed);
    }

    [TestMethod]
    public void Provisioning_confirmation_is_explicit()
    {
        var provisioning = new FakeProvisioningService();
        var application = CreateApplication(provisioning: provisioning);

        var operationId = application.ConfirmProvisioning("op-1");

        Assert.AreEqual("op-1", operationId);
        Assert.IsTrue(provisioning.Confirmed);
    }

    [TestMethod]
    public void Recovery_only_returns_the_latest_operation_when_recoverable()
    {
        var latestCompleted = new ProvisioningOperation
        {
            OperationId = "completed",
            Status = "completed",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var olderFailed = new ProvisioningOperation
        {
            OperationId = "failed",
            Status = "failed",
            UpdatedAt = latestCompleted.UpdatedAt.AddMinutes(-1)
        };

        var application = CreateApplication(
            provisioning: new FakeProvisioningService(latestCompleted, olderFailed));

        Assert.IsNull(application.GetProvisioningRecovery());
    }

    [TestMethod]
    public void Recovery_returns_latest_failed_operation()
    {
        var olderRunning = new ProvisioningOperation
        {
            OperationId = "running",
            Status = "running",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var latestFailed = new ProvisioningOperation
        {
            OperationId = "failed",
            Status = "failed",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var application = CreateApplication(
            provisioning: new FakeProvisioningService(latestFailed, olderRunning));

        Assert.AreSame(latestFailed, application.GetProvisioningRecovery());
    }

    private static WorkspaceControlApplication CreateApplication(
        IProvisioningService? provisioning = null,
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

    private sealed class FakeProvisioningService(params ProvisioningOperation[] history) : IProvisioningService
    {
        public IReadOnlyList<ProfileManifest> GetProfiles() => [];
        public Task<ProvisioningPlan> GetPlanAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProvisioningPlan(
                profileId,
                profileId,
                "test-scan",
                [],
                [],
                DateTimeOffset.UtcNow));
        public bool Confirmed { get; private set; }

        public Task<ProvisioningOperation> CreateAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(history.FirstOrDefault() ?? new ProvisioningOperation
            {
                OperationId = "operation",
                ProfileId = profileId,
                Plan = new ProvisioningPlan(profileId, profileId, "test-scan", [], [], DateTimeOffset.UtcNow),
                Status = ProvisioningOperationStatuses.AwaitingConfirmation
            });

        public string Confirm(string operationId)
        {
            Confirmed = true;
            return operationId;
        }

        public Task RunAsync(string operationId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public ProvisioningOperation? Get(string operationId) =>
            history.FirstOrDefault(x => x.OperationId == operationId);
        public string Resume(string operationId) => operationId;
        public IReadOnlyList<ProvisioningOperation> GetHistory() => history;
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
