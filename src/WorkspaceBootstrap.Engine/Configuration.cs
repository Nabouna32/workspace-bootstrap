using System.Text.Json;

namespace WorkspaceBootstrap;

public sealed class ConfigurationStore
{
    public string Root { get; }
    public string ComponentsRoot { get; }
    public string ProfilesRoot { get; }

    public ConfigurationStore()
    {
        var root = LocateRepositoryRoot();
        if (root is null)
            throw new InvalidOperationException(
                "Configuration Windows introuvable. Le package doit contenir bootstrap/windows/components et bootstrap/windows/profiles.");

        Root = root;
        ComponentsRoot = Path.Combine(root, "bootstrap", "windows", "components");
        ProfilesRoot = Path.Combine(root, "bootstrap", "windows", "profiles");
    }

    public IReadOnlyDictionary<string, ComponentManifest> LoadComponents()
    {
        var catalogPath = Path.Combine(ComponentsRoot, "catalog.json");
        var catalog = JsonSerializer.Deserialize<Catalog>(
            File.ReadAllText(catalogPath), JsonDefaults.Options)
            ?? throw new InvalidOperationException("Catalog Windows invalide.");

        return catalog.Components
            .Select(id => JsonSerializer.Deserialize<ComponentManifest>(
                File.ReadAllText(Path.Combine(ComponentsRoot, id, "component.json")),
                JsonDefaults.Options)
                ?? throw new InvalidOperationException($"Manifest invalide : {id}"))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, ProfileManifest> LoadProfiles() =>
        Directory.EnumerateFiles(ProfilesRoot, "*.json")
            .Select(path => JsonSerializer.Deserialize<ProfileManifest>(
                File.ReadAllText(path), JsonDefaults.Options)
                ?? throw new InvalidOperationException($"Profil invalide : {Path.GetFileName(path)}"))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    private static string? LocateRepositoryRoot()
    {
        var candidates = new List<string>();
        AddAncestors(candidates, AppContext.BaseDirectory);
        AddAncestors(candidates, Directory.GetCurrentDirectory());

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path =>
                File.Exists(Path.Combine(path, "bootstrap", "windows", "components", "catalog.json")));
    }

    private static void AddAncestors(List<string> candidates, string? start)
    {
        var current = start;
        for (var i = 0; i < 10 && !string.IsNullOrWhiteSpace(current); i++)
        {
            candidates.Add(current);
            current = Directory.GetParent(current)?.FullName;
        }
    }

    private sealed record Catalog(string[] Components);
}
