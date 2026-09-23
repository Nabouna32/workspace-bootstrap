using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceControl.Application;
using WorkspaceControl.Application.Contracts;
using WorkspaceControl.Domain;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class SoftwareInventoryServiceTests
{
    [TestMethod]
    public async Task Merge_prefers_stronger_ownership_and_preserves_evidence()
    {
        var first = Item("shared", "Tool", "9.0", SoftwareOwnership.Unknown, "registry", false);
        var second = Item("shared", "Tool", "10.0", SoftwareOwnership.PackageManagerManaged, "winget", true);

        var service = new SoftwareInventoryService([
            new FakeSource("registry", first),
            new FakeSource("winget", second)
        ]);

        var snapshot = await service.ScanAsync();

        Assert.AreEqual(1, snapshot.Items.Count);
        var item = snapshot.Items[0];
        Assert.AreEqual("10.0", item.Version);
        Assert.AreEqual(SoftwareOwnership.PackageManagerManaged, item.Ownership);
        Assert.AreEqual(2, item.Evidence.Count);
        Assert.IsTrue(item.Evidence.Any(evidence => evidence.IsStrong));
    }

    [TestMethod]
    public async Task Failed_source_becomes_diagnostic_without_hiding_other_sources()
    {
        var service = new SoftwareInventoryService([
            new ThrowingSource("broken"),
            new FakeSource("working", Item("working", "Working", "1.0", SoftwareOwnership.Manual, "working", false))
        ]);

        var snapshot = await service.ScanAsync();

        Assert.AreEqual(1, snapshot.Items.Count);
        Assert.AreEqual("Working", snapshot.Items[0].Name);
        StringAssert.Contains(snapshot.Diagnostics.Single(), "broken");
    }

    [TestMethod]
    public async Task Cancellation_is_not_converted_to_a_diagnostic()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var service = new SoftwareInventoryService([
            new FakeSource("source", Item("id", "Name", "1.0", SoftwareOwnership.Unknown, "source", false))
        ]);

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => service.ScanAsync(cancellation.Token));
    }

    private static SoftwareItem Item(
        string id,
        string name,
        string version,
        SoftwareOwnership ownership,
        string source,
        bool strongEvidence) =>
        new(
            id,
            name,
            version,
            "Publisher",
            null,
            ownership,
            SoftwareScope.System,
            [SoftwareSignal.Installed],
            [new SoftwareEvidence(source, "installed", null, DateTimeOffset.UtcNow, strongEvidence)]);

    private sealed class FakeSource(string id, params SoftwareItem[] items) : ISoftwareInventorySource
    {
        public string Id => id;

        public Task<SoftwareInventorySourceResult> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SoftwareInventorySourceResult(items, []));
    }

    private sealed class ThrowingSource(string id) : ISoftwareInventorySource
    {
        public string Id => id;

        public Task<SoftwareInventorySourceResult> ScanAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
