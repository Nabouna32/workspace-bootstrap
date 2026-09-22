using Microsoft.UI.Xaml;

namespace WorkspaceControl.Desktop;

public sealed partial class DiagnosticsPage : Microsoft.UI.Xaml.Controls.Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
        DataContext = ((App)Application.Current).ViewModel;
    }
}
