using System.Diagnostics;

namespace WorkspaceBootstrap.Desktop;

public sealed class DesktopProcessLauncher
{
    public void LaunchInteractiveProvisioning(string repositoryRoot)
    {
        var executable = Environment.GetEnvironmentVariable("WORKSPACE_BOOTSTRAP_ENGINE");
        executable = !string.IsNullOrWhiteSpace(executable) && File.Exists(executable)
            ? executable
            : Path.Combine(@"C:\Dev\WorkspaceBootstrap\app", "WorkspaceBootstrap.Cli.exe");

        if (!File.Exists(executable))
            throw new FileNotFoundException(
                "WorkspaceBootstrap.Cli.exe introuvable. Publiez le CLI self-contained avant le lancement interactif.",
                executable);

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = true
        });
    }
}
