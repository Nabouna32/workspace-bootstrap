namespace WorkspaceControl.Infrastructure;

public sealed class WorkspacePaths
{
    public string Root { get; }
    public string CacheRoot { get; }
    public string InstallersRoot => Path.Combine(CacheRoot, "installers");
    public string MetadataRoot => Path.Combine(CacheRoot, "metadata");
    public string StagingRoot => Path.Combine(CacheRoot, "staging");
    public string StateRoot => Path.Combine(Root, "state");
    public string LogsRoot => Path.Combine(Root, "logs");

    public WorkspacePaths(string? root = null)
    {
        Root = Path.GetFullPath(
            string.IsNullOrWhiteSpace(root)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WorkspaceControl")
                : root);

        CacheRoot = Path.Combine(Root, "cache");

        foreach (var path in new[]
        {
            Root,
            CacheRoot,
            InstallersRoot,
            MetadataRoot,
            StagingRoot,
            StateRoot,
            LogsRoot
        })
        {
            Directory.CreateDirectory(path);
        }
    }
}
