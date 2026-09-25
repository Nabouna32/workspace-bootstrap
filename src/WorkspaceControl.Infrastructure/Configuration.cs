using System.Text.Json;

namespace WorkspaceControl.Infrastructure;

public sealed class ConfigurationStore
{
    public string Root { get; }
    public string ComponentsRoot { get; }
    public string ProfilesRoot { get; }
    public string WorkspaceRoot { get; }

    public ConfigurationStore(
        string? contentRoot = null,
        string? workspaceRoot = null)
    {
        Root = Path.GetFullPath(
            string.IsNullOrWhiteSpace(contentRoot)
                ? AppContext.BaseDirectory
                : contentRoot);

        ComponentsRoot = Path.Combine(Root, "bootstrap", "windows", "components");
        ProfilesRoot = Path.Combine(Root, "bootstrap", "windows", "profiles");
        WorkspaceRoot = Path.GetFullPath(
            workspaceRoot
            ?? Path.Combine(Root, "workspaces"));

        Directory.CreateDirectory(WorkspaceRoot);
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

    public WorkspaceManifest CreateWorkspace(
        string name,
        string description,
        IReadOnlyCollection<string> componentIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name is required.", nameof(name));

        if (name.Length > 256)
            throw new ArgumentException("Workspace name is too long.", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Workspace description is required.", nameof(description));

        if (description.Length > 2000)
            throw new ArgumentException("Workspace description is too long.", nameof(description));

        var components = LoadComponents();
        var requestedIds = componentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var componentId in requestedIds)
        {
            if (!components.ContainsKey(componentId))
                throw new InvalidOperationException($"Unknown application '{componentId}'.");
        }

        var id = $"workspace-{Guid.NewGuid():N}";
        var workspace = new WorkspaceManifest(
            id,
            name.Trim(),
            description.Trim(),
            SchemaVersion: 2,
            DesiredState: new DesiredStateManifest(
                requestedIds.Select(componentId => new ProfileApplication(componentId)).ToArray(),
                [],
                [],
                [],
                [],
                []));

        SaveWorkspace(workspace, overwrite: false);
        return workspace;
    }

    public void SaveWorkspace(WorkspaceManifest workspace, bool overwrite = true)
    {
        ValidateWorkspace(workspace);

        var builtInProfiles = LoadProfileDirectory(ProfilesRoot);
        if (builtInProfiles.ContainsKey(workspace.Id))
            throw new InvalidOperationException(
                $"Workspace id '{workspace.Id}' conflicts with a built-in profile.");

        var destination = Path.Combine(WorkspaceRoot, $"{workspace.Id}.json");
        if (File.Exists(destination) && !overwrite)
            throw new IOException($"Workspace '{workspace.Id}' already exists.");

        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(workspace, JsonDefaults.Options));
            File.Move(temporary, destination, overwrite: overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public string ExportProfile(string profileId, string destinationPath)
    {
        var profile = LoadProfiles().TryGetValue(profileId, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown profile: {profileId}");

        var fullPath = Path.GetFullPath(destinationPath);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The export destination has no parent directory.");

        Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(profile, JsonDefaults.Options);
        File.WriteAllText(fullPath, json);
        return fullPath;
    }

    public WorkspaceManifest ImportProfile(string sourcePath, bool overwrite = false)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Profile file was not found.", fullPath);

        var profile = JsonSerializer.Deserialize<WorkspaceManifest>(
            File.ReadAllText(fullPath), JsonDefaults.Options)
            ?? throw new InvalidOperationException("The imported profile is invalid.");

        ValidateProfile(profile, Path.GetFileName(fullPath));

        var destination = Path.Combine(ProfilesRoot, $"{profile.Id}.json");
        if (File.Exists(destination) && !overwrite)
            throw new IOException($"Profile '{profile.Id}' already exists.");

        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile, JsonDefaults.Options));
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        return profile;
    }

    public IReadOnlyDictionary<string, WorkspaceManifest> LoadProfiles()
    {
        var profiles = LoadProfileDirectory(ProfilesRoot);

        foreach (var workspace in LoadProfileDirectory(WorkspaceRoot))
        {
            if (!profiles.TryAdd(workspace.Key, workspace.Value))
            {
                throw new InvalidOperationException(
                    $"Workspace '{workspace.Key}' conflicts with an existing built-in profile.");
            }
        }

        return profiles;
    }

    private static Dictionary<string, WorkspaceManifest> LoadProfileDirectory(string root)
    {
        if (!Directory.Exists(root))
            return new Dictionary<string, WorkspaceManifest>(StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(root, "*.json")
            .Select(path =>
            {
                var profile = JsonSerializer.Deserialize<WorkspaceManifest>(
                    File.ReadAllText(path), JsonDefaults.Options)
                    ?? throw new InvalidOperationException(
                        $"Invalid profile: {Path.GetFileName(path)}");

                ValidateProfile(profile, Path.GetFileName(path));
                return profile;
            })
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateProfile(WorkspaceManifest profile, string sourceName)
    {
        if (profile.SchemaVersion is not (1 or 2))
            throw new InvalidOperationException(
                $"Unsupported profile schema version '{profile.SchemaVersion}' in {sourceName}.");

        if (!IsValidProfileId(profile.Id))
            throw new InvalidOperationException($"Profile id '{profile.Id}' is not a valid profile identifier.");

        if (profile.SchemaVersion == 1 && (profile.Components is null || profile.Components.Length == 0))
            throw new InvalidOperationException(
                $"Legacy profile '{sourceName}' must declare at least one component.");

        if (profile.SchemaVersion == 2)
        {
            if (profile.DesiredState is null)
                throw new InvalidOperationException(
                    $"Profile '{sourceName}' must declare desiredState for schema version 2.");

            if (profile.DesiredState.Applications is null ||
                profile.DesiredState.WindowsSettings is null ||
                profile.DesiredState.Policies is null ||
                profile.DesiredState.RegistrySettings is null ||
                profile.DesiredState.Optimizations is null ||
                profile.DesiredState.Conditions is null)
            {
                throw new InvalidOperationException(
                    $"Profile '{sourceName}' contains an incomplete desiredState.");
            }

            ValidateApplicationStates(profile, sourceName);
        }
    }

    private void ValidateWorkspace(WorkspaceManifest workspace)
    {
        ValidateProfile(workspace, $"{workspace.Id}.json");

        if (workspace.SchemaVersion != 2 || workspace.DesiredState is null)
            throw new InvalidOperationException("User workspaces must use desired-state schema version 2.");

        var duplicateIds = workspace.DesiredState.Applications
            .GroupBy(application => application.ComponentId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
            throw new InvalidOperationException(
                $"Workspace '{workspace.Id}' contains duplicate desired applications.");

        var components = new HashSet<string>(
            LoadComponents().Keys,
            StringComparer.OrdinalIgnoreCase);

        foreach (var application in workspace.DesiredState.Applications)
        {
            if (!components.Contains(application.ComponentId))
                throw new InvalidOperationException(
                    $"Workspace '{workspace.Id}' references unknown application '{application.ComponentId}'.");
        }
    }

    private static void ValidateApplicationStates(WorkspaceManifest profile, string sourceName)
    {
        if (profile.SchemaVersion < 2 || profile.DesiredState is null)
            return;

        var duplicateIds = profile.DesiredState.Applications
            .GroupBy(application => application.ComponentId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Profile '{sourceName}' contains duplicate desired applications: {string.Join(", ", duplicateIds)}.");
        }

        foreach (var application in profile.DesiredState.Applications)
        {
            if (!string.Equals(application.State, "present", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(application.State, "absent", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Profile '{sourceName}' contains unsupported application state '{application.State}' for '{application.ComponentId}'.");
            }
        }
    }

    private static bool IsValidProfileId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
            return false;

        if (!(id[0] >= 'a' && id[0] <= 'z') && !char.IsAsciiDigit(id[0]))
            return false;

        return id.All(character =>
            (character >= 'a' && character <= 'z') ||
            char.IsAsciiDigit(character) ||
            character is '.' or '_' or '-');
    }

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
