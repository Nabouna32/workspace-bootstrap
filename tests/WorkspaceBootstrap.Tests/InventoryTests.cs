using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceBootstrap;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class InventoryTests
{
    [TestMethod]
    public async Task Scanner_deduplicates_observations_and_resolves_strongest_ownership()
    {
        var detectedAt = DateTimeOffset.UtcNow;
        var provider = new FakeProvider(
            "test-provider",
            [
                Observation("app-1", "Tool", "1.0.0", InventoryOwnership.Unknown, detectedAt),
                Observation("app-1", "Tool", "1.0.0", InventoryOwnership.ProductManaged, detectedAt.AddSeconds(1))
            ]);

        var snapshot = await new InventoryScanner([provider]).ScanAsync();

        Assert.AreEqual(1, snapshot.Items.Count);
        Assert.AreEqual(2, snapshot.Items[0].Observations.Count);
        Assert.AreEqual(InventoryOwnership.ProductManaged, snapshot.Items[0].Ownership);
        Assert.IsTrue(snapshot.Items[0].Signals.Contains(InventorySignal.Installed));
        Assert.AreEqual(1, snapshot.ProviderDiagnostics.Count);
        Assert.IsTrue(snapshot.ProviderDiagnostics[0].Success);
    }

    [TestMethod]
    public async Task Scanner_marks_older_versions_within_a_family()
    {
        var provider = new FakeProvider(
            "test-provider",
            [
                Observation("app-1", "Tool 1", "2.0.0", InventoryOwnership.Manual, DateTimeOffset.UtcNow),
                Observation("app-2", "Tool 2", "1.0.0", InventoryOwnership.Manual, DateTimeOffset.UtcNow),
                Observation("other", "Other", "9.0.0", InventoryOwnership.Manual, DateTimeOffset.UtcNow, familyId: "other")
            ]);

        var snapshot = await new InventoryScanner([provider]).ScanAsync();

        var older = snapshot.Items.Single(x => x.Id == "app-2");
        var newest = snapshot.Items.Single(x => x.Id == "app-1");

        Assert.IsTrue(older.Signals.Contains(InventorySignal.OlderVersion));
        Assert.IsFalse(newest.Signals.Contains(InventorySignal.OlderVersion));
    }

    [TestMethod]
    public async Task Scanner_preserves_provider_failure_as_diagnostic()
    {
        var provider = new ThrowingProvider("broken-provider");

        var snapshot = await new InventoryScanner([provider]).ScanAsync();

        Assert.AreEqual(0, snapshot.Items.Count);
        Assert.AreEqual(1, snapshot.ProviderDiagnostics.Count);
        Assert.IsFalse(snapshot.ProviderDiagnostics[0].Success);
        Assert.AreEqual("broken-provider", snapshot.ProviderDiagnostics[0].ProviderId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.ProviderDiagnostics[0].Detail));
    }

    [TestMethod]
    public void Provisioning_plan_uses_inventory_to_report_installed_and_outdated_states()
    {
        var configuration = new ConfigurationStore();
        var component = configuration.LoadComponents().Values.First();

        var provider = new FakeProvider(
            "test-provider",
            [
                Observation(
                    $"inventory-{component.Id}",
                    component.Name,
                    "1.2.3",
                    InventoryOwnership.PackageManagerManaged,
                    DateTimeOffset.UtcNow,
                    providerId: component.PackageId!,
                    evidence:
                    [
                        new InventoryEvidence(
                            "available-update",
                            "A newer version is available.",
                            false,
                            "test")
                    ])
            ]);

        var engine = new ProvisioningEngine(
            configuration,
            new InstallerEngine(new WorkspacePaths()),
            new WorkspacePaths(),
            new InventoryScanner([provider]));

        var profile = configuration.LoadProfiles().Values
            .First(x => x.Components.Contains(component.Id, StringComparer.OrdinalIgnoreCase));

        var plan = System.Text.Json.JsonSerializer.SerializeToDocument(
            engine.Plan(profile.Id));

        var item = plan.RootElement
            .GetProperty("Items")
            .EnumerateArray()
            .Single(x => x.GetProperty("Id").GetString() == component.Id);

        Assert.AreEqual("OUTDATED", item.GetProperty("StateCode").GetString());
        Assert.AreEqual("update", item.GetProperty("ActionCode").GetString());
    }

    private static InventoryObservation Observation(
        string id,
        string displayName,
        string? version,
        InventoryOwnership ownership,
        DateTimeOffset detectedAt,
        string? familyId = "tool",
        string? providerId = null,
        IReadOnlyList<InventoryEvidence>? evidence = null) =>
        new(
            id,
            displayName,
            version,
            familyId,
            "test-provider",
            providerId ?? id,
            "test",
            InventoryScope.System,
            null,
            null,
            null,
            ownership,
            Array.Empty<string>(),
            evidence ?? Array.Empty<InventoryEvidence>(),
            detectedAt);

    private sealed class FakeProvider(
        string id,
        IReadOnlyList<InventoryObservation> observations) : IInventoryProvider
    {
        public string Id => id;

        public Task<InventoryProviderResult> ScanAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new InventoryProviderResult(
                    observations,
                    new InventoryProviderDiagnostic(
                        Id,
                        true,
                        "Test provider completed.")));
    }

    private sealed class ThrowingProvider(string id) : IInventoryProvider
    {
        public string Id => id;

        public Task<InventoryProviderResult> ScanAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic provider failure.");
    }
}
