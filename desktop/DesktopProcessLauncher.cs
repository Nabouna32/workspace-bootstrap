using System.Diagnostics;

namespace WorkspaceBootstrap.Desktop;

public sealed class DesktopProcessLauncher
{
    public void LaunchInteractiveProvisioning(string repositoryRoot)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "WorkspaceBootstrap.Cli.exe");

        if (!File.Exists(executable))
            throw new FileNotFoundException(
                "WorkspaceBootstrap.Cli.exe introuvable dans le package de l’application.",
                executable);

        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = true
        });
    }
}
