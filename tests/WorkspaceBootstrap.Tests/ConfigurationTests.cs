using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceBootstrap;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public void Components_are_loadable_and_have_unique_ids()
    {
        var configuration = new ConfigurationStore();
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
        var configuration = new ConfigurationStore();
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
}


[TestClass]
public sealed class ProvisioningPlanContractTests
{
    [TestMethod]
    public void Provisioning_plan_exposes_desktop_contract_fields()
    {
        var configuration = new ConfigurationStore();
        var engine = new ProvisioningEngine(
            configuration,
            new InstallerEngine(new WorkspacePaths()),
            new WorkspacePaths());

        var plan = engine.Plan(configuration.LoadProfiles().Values.First().Id);
        using var document = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(plan));

        foreach (var item in document.RootElement.GetProperty("Items").EnumerateArray())
        {
            Assert.IsTrue(item.TryGetProperty("StateCode", out var state));
            Assert.IsFalse(string.IsNullOrWhiteSpace(state.GetString()));

            Assert.IsTrue(item.TryGetProperty("ActionCode", out var action));
            Assert.IsFalse(string.IsNullOrWhiteSpace(action.GetString()));

            Assert.IsTrue(item.TryGetProperty("Message", out var message));
            Assert.IsFalse(string.IsNullOrWhiteSpace(message.GetString()));
            Assert.AreEqual("UNKNOWN", state.GetString());
        }
    }
}
