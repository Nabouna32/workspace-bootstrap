using WorkspaceControl.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceControl.Infrastructure;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public void Components_are_loadable_and_have_unique_ids()
    {
        var configuration = new ConfigurationStore(FindRepositoryRoot());
        var components = configuration.LoadComponents();

        Assert.IsNotEmpty(components);
        Assert.AreEqual(
            components.Count,
            components.Values.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var component in components.Values)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(component.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(component.Name));
            Assert.IsFalse(string.IsNullOrWhiteSpace(component.PackageId));
            Assert.IsTrue(
                component.OfficialSource is not null ||
                string.Equals(component.FallbackPackageManager, "winget", StringComparison.OrdinalIgnoreCase),
                $"Component {component.Id} has neither an official source nor the explicit WinGet fallback.");

            if (component.OfficialSource is not null)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(component.OfficialSource.Type));
                CollectionAssert.Contains(
                    new[] { "github-release", "rarlab-localized", "chrome-enterprise" },
                    component.OfficialSource.Type,
                    $"Unsupported official source type for {component.Id}: {component.OfficialSource.Type}");

                if (component.OfficialSource.Type == "github-release")
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(component.OfficialSource.Repository));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(component.OfficialSource.AssetRegex));
                }
            }
        }
    }

    [TestMethod]
    public void Profiles_are_loadable_and_reference_known_components()
    {
        var configuration = new ConfigurationStore(FindRepositoryRoot());
        var components = configuration.LoadComponents();
        var profiles = configuration.LoadProfiles();

        Assert.IsNotEmpty(profiles);
        Assert.AreEqual(
            profiles.Count,
            profiles.Values.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var profile in profiles.Values)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.Name));
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.Description));
            Assert.IsNotEmpty(profile.Components);

            Assert.AreEqual(
                profile.Components.Length,
                profile.Components.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                $"{profile.Id} contains duplicate components.");

            foreach (var componentId in profile.Components)
                Assert.IsTrue(components.ContainsKey(componentId), $"{profile.Id} references unknown component {componentId}.");
        }

        foreach (var component in components.Values)
        {
            foreach (var profileId in component.Profiles ?? Array.Empty<string>())
                Assert.IsTrue(profiles.ContainsKey(profileId), $"{component.Id} references unknown profile {profileId}.");
        }
    }

    [TestMethod]
    public async Task Provisioning_plan_blocks_mutation_when_inventory_is_incomplete()
    {
        var configuration = new ConfigurationStore(FindRepositoryRoot());
        var paths = new WorkspacePaths(Path.Combine(Path.GetTempPath(), "workspace-bootstrap-plan-tests", Guid.NewGuid().ToString("N")));
        var inventory = new InventoryScanner([new FailedInventoryProvider()]);
        var engine = new ProvisioningEngine(
            configuration,
            new InstallerEngine(paths),
            paths,
            inventory);

        var plan = await engine.PlanAsync(configuration.LoadProfiles().Values.First().Id);

        Assert.IsNotEmpty(plan.Items);
        Assert.IsTrue(plan.Items.All(item =>
            item.StateCode == "UNKNOWN" &&
            item.ActionCode == "blocked"));
    }

    private sealed class FailedInventoryProvider : IInventoryProvider
    {
        public string Id => "test.failed";

        public Task<InventoryProviderResult> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new InventoryProviderResult(
                [],
                new InventoryProviderDiagnostic(
                    Id,
                    false,
                    "Synthetic inventory failure.",
                    "Test failure.")));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bootstrap", "windows", "components", "catalog.json")))
                return current.FullName;

            current = current.Parent;
        }

        Assert.Fail("Repository content root could not be located for the configuration contract test.");
        return string.Empty;
    }
}


[TestClass]
public sealed class ProvisioningPlanContractTests
{
    [TestMethod]
    public async Task Provisioning_plan_exposes_typed_contract_fields()
    {
        var configuration = new ConfigurationStore(FindRepositoryRoot());
        var paths = new WorkspacePaths(Path.Combine(Path.GetTempPath(), "workspace-bootstrap-plan-tests", Guid.NewGuid().ToString("N")));
        var engine = new ProvisioningEngine(
            configuration,
            new InstallerEngine(paths),
            paths,
            new InventoryScanner([]));

        var plan = await engine.PlanAsync(configuration.LoadProfiles().Values.First().Id);
        Assert.AreEqual(configuration.LoadProfiles().Values.First().Id, plan.ProfileId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(plan.InventoryScanId));
        Assert.IsNotEmpty(plan.Items);

        foreach (var item in plan.Items)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ComponentId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ComponentName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.StateCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.ActionCode));
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.Message));
            Assert.IsTrue(item.StateCode is "MISSING" or "INSTALLED" or "OUTDATED" or "UNKNOWN");
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bootstrap", "windows", "components", "catalog.json")))
                return current.FullName;

            current = current.Parent;
        }

        Assert.Fail("Repository content root could not be located for the configuration contract test.");
        return string.Empty;
    }
}


[TestClass]
public sealed class InstallerEngineTests
{
    [TestMethod]
    public void WinGet_install_action_uses_install_command()
    {
        var component = new ComponentManifest(
            "test-component",
            "Test component",
            null,
            "Test.Package",
            null,
            "exe",
            null,
            "x64",
            null,
            [],
            "winget");

        var arguments = InstallerEngine.BuildWingetArguments(
            component,
            ProvisioningActionCodes.Install);

        Assert.AreEqual("install", arguments[0]);
        CollectionAssert.Contains(arguments.ToArray(), "Test.Package");
    }

    [TestMethod]
    public void WinGet_update_action_uses_upgrade_command()
    {
        var component = new ComponentManifest(
            "test-component",
            "Test component",
            null,
            "Test.Package",
            null,
            "exe",
            null,
            "x64",
            null,
            [],
            "winget");

        var arguments = InstallerEngine.BuildWingetArguments(
            component,
            ProvisioningActionCodes.Update);

        Assert.AreEqual("upgrade", arguments[0]);
        CollectionAssert.Contains(arguments.ToArray(), "Test.Package");
    }
}
