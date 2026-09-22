using Microsoft.UI.Xaml;

namespace WorkspaceControl.Desktop;

public sealed partial class ProfilesPage : Microsoft.UI.Xaml.Controls.Page
{
    public ProfilesPage()
    {
        InitializeComponent();
        DataContext = ((App)Microsoft.UI.Xaml.Application.Current).ViewModel;
    }
}
