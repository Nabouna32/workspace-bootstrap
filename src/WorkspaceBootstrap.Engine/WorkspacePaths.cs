namespace WorkspaceBootstrap;

public sealed class WorkspacePaths
{
    public string Root { get; } = @"C:\Dev\WorkspaceBootstrap";
    public string CacheRoot { get; }
    public string InstallersRoot => Path.Combine(CacheRoot, "installers");
    public string MetadataRoot => Path.Combine(CacheRoot, "metadata");
    public string StagingRoot => Path.Combine(CacheRoot, "staging");
    public string StateRoot => Path.Combine(Root, "state");
    public string LogsRoot => Path.Combine(Root, "logs");

    public WorkspacePaths()
    {
        CacheRoot = Environment.GetEnvironmentVariable("DEV_ENVIRONMENT_CACHE")
            ?? @"C:\DevCache";

        foreach (var path in new[] { Root, CacheRoot, InstallersRoot, MetadataRoot, StagingRoot, StateRoot, LogsRoot })
            Directory.CreateDirectory(path);
    }
}
