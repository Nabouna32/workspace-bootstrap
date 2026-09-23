using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceControl.Domain;
using WorkspaceControl.Infrastructure;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class ProfilePersistenceTests
{
    [TestMethod]
    public void Import_rejects_path_traversal_profile_identifier()
    {
        using var fixture = new ProfilePersistenceFixture();
        var source = fixture.WriteImportProfile("../outside");

        Assert.ThrowsException<InvalidOperationException>(() =>
            fixture.Configuration.ImportProfile(source));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "outside.json")));
    }

    [TestMethod]
    public void Import_does_not_overwrite_existing_profile_without_explicit_opt_in()
    {
        using var fixture = new ProfilePersistenceFixture();
        fixture.WriteStoredProfile("managed");
        var source = fixture.WriteImportProfile("managed", "Imported");

        Assert.ThrowsException<IOException>(() =>
            fixture.Configuration.ImportProfile(source));

        var stored = fixture.ReadStoredProfile("managed");
        Assert.AreEqual("Stored", stored.Name);
    }

    [TestMethod]
    public void Import_replaces_existing_profile_when_overwrite_is_explicit()
    {
        using var fixture = new ProfilePersistenceFixture();
        fixture.WriteStoredProfile("managed");
        var source = fixture.WriteImportProfile("managed", "Imported");

        var imported = fixture.Configuration.ImportProfile(source, overwrite: true);

        Assert.AreEqual("Imported", imported.Name);
        Assert.AreEqual("Imported", fixture.ReadStoredProfile("managed").Name);
    }

    [TestMethod]
    public void Export_writes_the_requested_profile()
    {
        using var fixture = new ProfilePersistenceFixture();
        fixture.WriteStoredProfile("managed", "Export me");
        var destination = Path.Combine(fixture.Root, "exports", "managed.json");

        var exportedPath = fixture.Configuration.ExportProfile("managed", destination);

        Assert.AreEqual(Path.GetFullPath(destination), exportedPath);
        Assert.IsTrue(File.Exists(destination));
        Assert.AreEqual("Export me", fixture.ReadProfileFile(destination).Name);
    }

    private sealed class ProfilePersistenceFixture : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            "workspace-control-profile-tests",
            Guid.NewGuid().ToString("N"));

        public ConfigurationStore Configuration { get; }

        public ProfilePersistenceFixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "bootstrap", "windows", "components"));
            Directory.CreateDirectory(Path.Combine(Root, "bootstrap", "windows", "profiles"));
            File.WriteAllText(
                Path.Combine(Root, "bootstrap", "windows", "components", "catalog.json"),
                """{"components":[]}""");

            Configuration = new ConfigurationStore(Root);
        }

        public string WriteStoredProfile(string id, string name = "Stored")
        {
            return WriteProfile(
                Path.Combine(Root, "bootstrap", "windows", "profiles", $"{id}.json"),
                id,
                name);
        }

        public string WriteImportProfile(string id, string name = "Imported")
        {
            return WriteProfile(
                Path.Combine(Root, "incoming.json"),
                id,
                name);
        }

        public ProfileManifest ReadStoredProfile(string id) =>
            ReadProfileFile(Path.Combine(Root, "bootstrap", "windows", "profiles", $"{id}.json"));

        public ProfileManifest ReadProfileFile(string path) =>
            JsonSerializer.Deserialize<ProfileManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Test profile could not be read.");

        private static string WriteProfile(string path, string id, string name)
        {
            var profile = new ProfileManifest(
                id,
                name,
                "Profile persistence test",
                DesiredState: new DesiredStateManifest([], [], [], [], [], []),
                SchemaVersion: 2);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
