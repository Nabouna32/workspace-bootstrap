using Microsoft.UI.Xaml;
using WorkspaceBootstrap;

namespace WorkspaceControl.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Engine = new EngineFacade();
        ViewModel = new WorkspaceControlViewModel(Engine);
    }

    public EngineFacade Engine { get; }
    public WorkspaceControlViewModel ViewModel { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= new MainWindow();
        _window.Activate();
    }
}
