using Microsoft.UI.Xaml;

namespace WorkspaceControl.Desktop;

public sealed partial class WindowsPage : Microsoft.UI.Xaml.Controls.Page
{
    public WindowsPage()
    {
        InitializeComponent();
        DataContext = ((App)Application.Current).ViewModel;
    }
}
