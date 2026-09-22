using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkspaceControl.Desktop;

public sealed partial class OverviewPage : Page
{
    public OverviewPage()
    {
        InitializeComponent();
        DataContext = ((App)Application.Current).ViewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await ((WorkspaceControlViewModel)DataContext).RefreshAsync();
}
