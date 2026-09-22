using Microsoft.UI.Xaml;

namespace WorkspaceControl.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window ??= new MainWindow();
        _window.Activate();
    }
}
