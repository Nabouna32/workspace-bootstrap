using System.IO;
using System.Windows;

namespace WorkspaceBootstrap.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var repositoryRoot = FindRepositoryRoot();
        var engineClient = new DesktopEngineClient(repositoryRoot);
        var jobStore = new JobStore();
        var capabilityProvider = new DesktopCapabilitySnapshotProvider(engineClient);
        var jobScheduler = new JobScheduler(jobStore, capabilityProvider);

        DataContext = new MainViewModel(
            repositoryRoot,
            engineClient,
            new DesktopProcessLauncher(),
            new DesktopDialogService(),
            jobStore,
            jobScheduler);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dev-env.bat")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
