using System.Text.Json;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class RepositoryPolicyTests
{
    [TestMethod]
    public void Retired_script_files_are_absent()
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
    public void Workflows_use_only_github_hosted_windows_runners()
    {
        var root = LocateRepositoryRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");
        var workflows = Directory.EnumerateFiles(workflowsRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.IsNotEmpty(workflows);

        foreach (var workflow in workflows)
        {
            var content = File.ReadAllText(workflow);
            StringAssert.DoesNotContain(content, "self-hosted");
            StringAssert.Contains(content, "runs-on: windows-latest");
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
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;

            Assert.IsFalse(rootElement.TryGetProperty("installer", out _), path);
            Assert.IsFalse(rootElement.TryGetProperty("packageManager", out _), path);

            if (rootElement.TryGetProperty("fallbackPackageManager", out var fallback))
                Assert.AreEqual("winget", fallback.GetString(), path);
        }
    }

    [TestMethod]
    public void Product_identity_is_not_the_retired_dev_environment()
    {
        var root = LocateRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"));

        foreach (var path in files)
        {
            var extension = Path.GetExtension(path);
            if (!new[] { ".cs", ".csproj", ".xaml", ".json", ".md", ".yml", ".yaml" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase))
                continue;

            var content = File.ReadAllText(path);
            StringAssert.DoesNotContain(content, "BounaDevEnvironment", path);
            StringAssert.DoesNotContain(content, "Bouna Dev Environment", path);
        }
    }


    [TestMethod]
    public void Workflows_do_not_depend_on_shell_scripts_for_product_execution()
    {
        var root = LocateRepositoryRoot();
        var workflowsRoot = Path.Combine(root, ".github", "workflows");

        foreach (var workflow in Directory.EnumerateFiles(workflowsRoot, "*.y*ml", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(workflow);
            StringAssert.DoesNotContain(content, "bootstrap\\windows\\", workflow);
            StringAssert.DoesNotContain(content, "powershell -File", workflow);
            StringAssert.DoesNotContain(content, "pwsh -File", workflow);
            StringAssert.DoesNotContain(content, "self-hosted", workflow);
        }
    }

    [TestMethod]
    public void Desktop_has_explicit_theme_modes()
    {
        var root = LocateRepositoryRoot();
        var manager = File.ReadAllText(Path.Combine(root, "desktop", "ThemeManager.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "desktop", "MainViewModel.cs"));

        StringAssert.Contains(manager, "ThemeMode.System");
        StringAssert.Contains(manager, "ThemeMode.Light");
        StringAssert.Contains(manager, "ThemeMode.Dark");
        StringAssert.Contains(viewModel, "ThemeOptions");
        StringAssert.Contains(viewModel, "SelectedTheme");
    }

    private static string LocateRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md"))
                && Directory.Exists(Path.Combine(current.FullName, "src"))
                && Directory.Exists(Path.Combine(current.FullName, "desktop")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
