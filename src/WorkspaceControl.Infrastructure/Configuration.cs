using System.Text.Json;

namespace WorkspaceControl.Infrastructure;

public sealed class ConfigurationStore
{
    public string Root { get; }
    public string ComponentsRoot { get; }
    public string TemplatesRoot { get; }
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
        TemplatesRoot = Path.Combine(Root, "bootstrap", "windows", "profiles");
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
                requestedIds.Select(componentId => new WorkspaceApplication(componentId)).ToArray(),
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

        var templates = LoadWorkspaceTemplates();
        if (templates.ContainsKey(workspace.Id))
            throw new InvalidOperationException(
                $"Workspace id '{workspace.Id}' conflicts with a built-in workspace template.");

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

    public string ExportWorkspace(string workspaceId, string destinationPath)
    {
        var workspace = LoadWorkspaces().TryGetValue(workspaceId, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown workspace: {workspaceId}");

        var fullPath = Path.GetFullPath(destinationPath);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The export destination has no parent directory.");

        Directory.CreateDirectory(parent);

        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(workspace, JsonDefaults.Options));

        return fullPath;
    }

    public WorkspaceManifest ImportWorkspace(string sourcePath, bool overwrite = false)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Workspace file was not found.", fullPath);

        var workspace = JsonSerializer.Deserialize<WorkspaceManifest>(
            File.ReadAllText(fullPath), JsonDefaults.Options)
            ?? throw new InvalidOperationException("The imported workspace is invalid.");

        ValidateWorkspace(workspace);

        var destination = Path.Combine(WorkspaceRoot, $"{workspace.Id}.json");
        if (File.Exists(destination) && !overwrite)
            throw new IOException($"Workspace '{workspace.Id}' already exists.");

        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(workspace, JsonDefaults.Options));
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        return workspace;
    }

    public IReadOnlyDictionary<string, WorkspaceManifest> LoadWorkspaces() =>
        LoadManifestDirectory(WorkspaceRoot);

    public IReadOnlyDictionary<string, WorkspaceManifest> LoadWorkspaceTemplates() =>
        LoadManifestDirectory(TemplatesRoot);

    private static Dictionary<string, WorkspaceManifest> LoadManifestDirectory(string root)
    {
        if (!Directory.Exists(root))
            return new Dictionary<string, WorkspaceManifest>(StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(root, "*.json")
            .Select(path =>
            {
                var workspace = JsonSerializer.Deserialize<WorkspaceManifest>(
                    File.ReadAllText(path), JsonDefaults.Options)
                    ?? throw new InvalidOperationException(
                        $"Invalid workspace manifest: {Path.GetFileName(path)}");

                ValidateWorkspaceManifest(workspace, Path.GetFileName(path));
                return workspace;
            })
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateWorkspaceManifest(WorkspaceManifest workspace, string sourceName)
    {
        if (workspace.SchemaVersion is not (1 or 2))
            throw new InvalidOperationException(
                $"Unsupported workspace schema version '{workspace.SchemaVersion}' in {sourceName}.");

        if (!IsValidWorkspaceId(workspace.Id))
            throw new InvalidOperationException(
                $"Workspace id '{workspace.Id}' is not a valid workspace identifier.");

        if (workspace.SchemaVersion == 1 && (workspace.Components is null || workspace.Components.Length == 0))
            throw new InvalidOperationException(
                $"Legacy workspace template '{sourceName}' must declare at least one component.");

        if (workspace.SchemaVersion == 2)
        {
            if (workspace.DesiredState is null)
                throw new InvalidOperationException(
                    $"Workspace '{sourceName}' must declare desiredState for schema version 2.");

            if (workspace.DesiredState.Applications is null ||
                workspace.DesiredState.WindowsSettings is null ||
                workspace.DesiredState.Policies is null ||
                workspace.DesiredState.RegistrySettings is null ||
                workspace.DesiredState.Optimizations is null ||
                workspace.DesiredState.Conditions is null)
            {
                throw new InvalidOperationException(
                    $"Workspace '{sourceName}' contains an incomplete desiredState.");
            }

            ValidateApplicationStates(workspace, sourceName);
        }
    }

    private void ValidateWorkspace(WorkspaceManifest workspace)
    {
        ValidateWorkspaceManifest(workspace, $"{workspace.Id}.json");

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

    private static void ValidateApplicationStates(
        WorkspaceManifest workspace,
        string sourceName)
    {
        if (workspace.SchemaVersion < 2 || workspace.DesiredState is null)
            return;

        var duplicateIds = workspace.DesiredState.Applications
            .GroupBy(application => application.ComponentId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Workspace '{sourceName}' contains duplicate desired applications: {string.Join(", ", duplicateIds)}.");
        }

        foreach (var application in workspace.DesiredState.Applications)
        {
            if (!string.Equals(application.State, "present", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(application.State, "absent", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Workspace '{sourceName}' contains unsupported application state '{application.State}' for '{application.ComponentId}'.");
            }
        }
    }

    private static bool IsValidWorkspaceId(string id)
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
        if (!File.Exists(catalogPath) || !Directory.Exists(TemplatesRoot))
        {
            throw new InvalidOperationException(
                $"Workspace Control content is incomplete. Expected '{ComponentsRoot}' and '{TemplatesRoot}'.");
        }
    }

    private sealed record Catalog(string[] Components);
}
