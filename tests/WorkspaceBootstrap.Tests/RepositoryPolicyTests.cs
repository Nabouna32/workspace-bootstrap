[TestClass]
public sealed class RepositoryPolicyTests
{
    [TestMethod]
    public void Legacy_script_files_are_absent()
    {
        var root = LocateRepositoryRoot();
        var legacy = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .Where(path => new[] { ".ps1", ".bat", ".cmd", ".sh" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.IsEmpty(legacy, string.Join(Environment.NewLine, legacy));
    }

    [TestMethod]
    public void Workflows_target_only_the_self_hosted_windows_runner()
    {
        var root = LocateRepositoryRoot();
        var workflows = Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml");

        foreach (var workflow in workflows)
        {
            var content = File.ReadAllText(workflow);
            StringAssert.DoesNotContain(content, "ubuntu-latest");
            StringAssert.DoesNotContain(content, "windows-latest");
            StringAssert.DoesNotContain(content, "macos-latest");
            StringAssert.Contains(content, "runs-on: [self-hosted, windows, x64]");
        }
    }

    [TestMethod]
    public void Component_manifests_do_not_reference_retired_script_installers()
    {
        var root = LocateRepositoryRoot();
        var components = Directory.EnumerateFiles(
            Path.Combine(root, "bootstrap", "windows", "components"),
            "component.json",
            SearchOption.AllDirectories);

        foreach (var path in components)
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;

            Assert.IsFalse(rootElement.TryGetProperty("installer", out _), path);
            Assert.IsFalse(rootElement.TryGetProperty("packageManager", out _), path);

            if (rootElement.TryGetProperty("fallbackPackageManager", out var fallback))
                Assert.AreEqual("winget", fallback.GetString(), path);
        }
    }

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
