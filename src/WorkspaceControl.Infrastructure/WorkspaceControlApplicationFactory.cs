using WorkspaceControl.Application;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Infrastructure;

public static class WorkspaceControlApplicationFactory
{
    public static IWorkspaceControlApplication Create()
    {
        var paths = new WorkspacePaths();
        var configuration = new ConfigurationStore();
        var installer = new InstallerEngine(paths);
        var inventory = new InventoryScanner([
            new WindowsRegistryUninstallInventoryProvider(),
            new WinGetInventoryProvider()
        ]);
        var workspaceOperations = new WorkspaceOperationEngine(configuration, installer, paths, inventory);

        return new WorkspaceControlApplication(
            new Application.WorkspaceOperationServiceAdapter(workspaceOperations),
            new Application.InventoryServiceAdapter(inventory),
            new SoftwareInventoryService([
                new Software.RegistrySoftwareInventorySource(),
                new Software.WinGetSoftwareInventorySource()
            ]),
            new Application.WindowsAdministrationServiceAdapter(),
            new Application.OptimizationServiceAdapter());
    }
}
