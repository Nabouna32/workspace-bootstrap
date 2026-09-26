using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceControl.Domain;
using WorkspaceControl.Infrastructure;

namespace WorkspaceControl.Tests;

[TestClass]
public sealed class WinGetInventoryProviderTests
{
    [TestMethod]
    public void ParseListOutput_extracts_installed_package_and_available_update()
    {
        var output = Table(
            ["7-Zip", "7zip.7zip", "24.09", "25.00", "winget"],
            ["Git", "Git.Git", "2.49.0", "2.50.1", "winget"]);

        var detectedAt = DateTimeOffset.Parse("2026-09-23T05:00:00Z");
        var items = WinGetInventoryProvider.ParseListOutput(output, detectedAt);

        Assert.AreEqual(2, items.Count);

        var sevenZip = items.Single(x => x.DisplayName == "7-Zip");
        Assert.AreEqual("24.09", sevenZip.Version);
        Assert.AreEqual("25.00", sevenZip.AvailableVersion);
        Assert.AreEqual("winget:7zip.7zip", sevenZip.ProviderId);
        Assert.AreEqual(InventoryScope.Unknown, sevenZip.Scope);
        Assert.IsNotNull(sevenZip.RemovalCapability);
        Assert.AreEqual(RemovalCapabilityKindCodes.WinGet, sevenZip.RemovalCapability!.Kind);
        Assert.AreEqual("7zip.7zip", sevenZip.RemovalCapability.PackageId);
        Assert.AreEqual("winget", sevenZip.RemovalCapability.Source);
        Assert.IsTrue(sevenZip.Evidence.Any(x => x.Kind == "available-update"));
        Assert.AreEqual(detectedAt, sevenZip.DetectedAtUtc);
    }

    [TestMethod]
    public void ParseListOutput_does_not_invent_available_version_when_column_is_unknown()
    {
        var output = Table(
            ["Example App", "Example.App", "1.0.0", "Unknown", "winget"]);

        var items = WinGetInventoryProvider.ParseListOutput(output, DateTimeOffset.UtcNow);

        Assert.AreEqual(1, items.Count);
        Assert.IsNull(items[0].AvailableVersion);
        Assert.IsFalse(items[0].Evidence.Any(x => x.Kind == "available-update"));
    }

    [TestMethod]
    public void ParseListOutput_preserves_source_identity_for_store_entries()
    {
        var output = Table(
            ["Example App", "Example.App", "1.0.0", "", "msstore"]);

        var items = WinGetInventoryProvider.ParseListOutput(output, DateTimeOffset.UtcNow);

        Assert.AreEqual(1, items.Count);
        var item = items[0];
        Assert.AreEqual("msstore:Example.App", item.ProviderId);
        Assert.AreEqual("msstore", item.Source);
    }

    [TestMethod]
    public void ParseListOutput_rejects_output_without_column_separator()
    {
        const string output = "Name Id Version\nExample App Example.App 1.0.0";

        var items = WinGetInventoryProvider.ParseListOutput(output, DateTimeOffset.UtcNow);

        Assert.IsEmpty(items);
    }

    [TestMethod]
    public void ParseListOutput_keeps_side_by_side_versions_distinct()
    {
        var output = Table(
            ["Runtime", "Runtime.App", "1.0.0", "", "winget"],
            ["Runtime", "Runtime.App", "2.0.0", "", "winget"]);

        var items = WinGetInventoryProvider.ParseListOutput(output, DateTimeOffset.UtcNow);

        Assert.AreEqual(2, items.Count);
        CollectionAssert.AreEquivalent(
            new[] { "1.0.0", "2.0.0" },
            items.Select(x => x.Version).ToArray());
    }

    private static string Table(params string[][] rows)
    {
        var header = new[] { "Name", "Id", "Version", "Available", "Source" };
        var widths = new[] { 20, 19, 14, 14, 6 };
        string Format(string[] values) =>
            string.Join(" ", values.Select((value, index) => value.PadRight(widths[index]))).TrimEnd();

        var separator = string.Join(" ", widths.Select(width => new string('-', width)));
        return string.Join("\n", new[] { Format(header), separator }.Concat(rows.Select(Format)));
    }
}
