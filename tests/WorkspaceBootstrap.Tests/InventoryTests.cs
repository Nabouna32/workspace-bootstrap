using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceBootstrap;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class InventoryTests
{
    [TestMethod]
    public async Task Scanner_DeduplicatesObservations_AndMarksOlderVersions()
    {
        var now = DateTimeOffset.UtcNow;
        var provider = new FakeInventoryProvider(
            "test",
            [
                Observation("tool.one", "Tool", "2.0.0", now),
                Observation("tool.one", "Tool", "1.0.0", now.AddSeconds(-1))
            ]);

        var snapshot = await new InventoryScanner([provider]).ScanAsync();

        Assert.AreEqual(2, snapshot.Items.Count);
        Assert.IsTrue(snapshot.Items.Single(item => item.Version == "1.0.0").Signals.Contains(InventorySignal.OlderVersion));
        Assert.IsFalse(snapshot.Items.Single(item => item.Version == "2.0.0").Signals.Contains(InventorySignal.OlderVersion));
    }

    [TestMethod]
    public async Task Scanner_PreservesProviderFailureAsDiagnostic()
    {
        var provider = new ThrowingInventoryProvider("broken");
        var snapshot = await new InventoryScanner([provider]).ScanAsync();

        Assert.AreEqual(0, snapshot.Items.Count);
        var diagnostic = snapshot.ProviderDiagnostics.Single();
        Assert.AreEqual("broken", diagnostic.ProviderId);
        Assert.IsFalse(diagnostic.Success);
    }

    private static InventoryObservation Observation(
        string id,
        string name,
        string version,
        DateTimeOffset detectedAt) =>
        new(
            id,
            name,
            version,
            "test-family",
            "test",
            id,
            "test",
            InventoryScope.User,
            null,
            null,
            null,
            InventoryOwnership.Unknown,
            [],
            [],
            detectedAt);

    private sealed class FakeInventoryProvider(
        string id,
        IReadOnlyList<InventoryObservation> observations) : IInventoryProvider
    {
        public string Id => id;

        public Task<InventoryProviderResult> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new InventoryProviderResult(
                observations,
                new InventoryProviderDiagnostic(Id, true, "ok")));
    }

    private sealed class ThrowingInventoryProvider(string id) : IInventoryProvider
    {
        public string Id => id;

        public Task<InventoryProviderResult> ScanAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
