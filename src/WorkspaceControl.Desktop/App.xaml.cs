using Microsoft.UI.Xaml;
using WorkspaceControl.Application;
using WorkspaceBootstrap;

namespace WorkspaceControl.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Application = WorkspaceControlApplicationFactory.Create();
        ViewModel = new WorkspaceControlViewModel(Application);
    }

    public IWorkspaceControlApplication Application { get; }
    public WorkspaceControlViewModel ViewModel { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= new MainWindow();
        _window.Activate();
    }
}
