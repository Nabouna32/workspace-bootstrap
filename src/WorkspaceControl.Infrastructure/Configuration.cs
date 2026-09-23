using System.Text.Json;

namespace WorkspaceControl.Infrastructure;

public sealed class ConfigurationStore
{
    public string Root { get; }
    public string ComponentsRoot { get; }
    public string ProfilesRoot { get; }

    public ConfigurationStore(string? contentRoot = null)
    {
        Root = Path.GetFullPath(
            string.IsNullOrWhiteSpace(contentRoot)
                ? AppContext.BaseDirectory
                : contentRoot);

        ComponentsRoot = Path.Combine(Root, "bootstrap", "windows", "components");
        ProfilesRoot = Path.Combine(Root, "bootstrap", "windows", "profiles");

        ValidateLayout();
    }

    public IReadOnlyDictionary<string, ComponentManifest> LoadComponents()
    {
        var catalogPath = Path.Combine(ComponentsRoot, "catalog.json");
        var catalog = JsonSerializer.Deserialize<Catalog>(
            File.ReadAllText(catalogPath), JsonDefaults.Options)
            ?? throw new InvalidOperationException("Windows catalog is invalid.");

        return catalog.Components
            .Select(id => JsonSerializer.Deserialize<ComponentManifest>(
                File.ReadAllText(Path.Combine(ComponentsRoot, id, "component.json")),
                JsonDefaults.Options)
                ?? throw new InvalidOperationException($"Invalid component manifest: {id}"))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, ProfileManifest> LoadProfiles() =>
        Directory.EnumerateFiles(ProfilesRoot, "*.json")
            .Select(path => JsonSerializer.Deserialize<ProfileManifest>(
                File.ReadAllText(path), JsonDefaults.Options)
                ?? throw new InvalidOperationException(
                    $"Invalid profile: {Path.GetFileName(path)}"))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    private void ValidateLayout()
    {
        var catalogPath = Path.Combine(ComponentsRoot, "catalog.json");
        if (!File.Exists(catalogPath) || !Directory.Exists(ProfilesRoot))
        {
            throw new InvalidOperationException(
                $"Workspace Control content is incomplete. Expected '{ComponentsRoot}' and '{ProfilesRoot}'.");
        }
    }

    private sealed record Catalog(string[] Components);
}
