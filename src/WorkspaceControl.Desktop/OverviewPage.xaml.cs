using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkspaceControl.Desktop;

public sealed partial class OverviewPage : Page
{
    public OverviewPage()
    {
        InitializeComponent();
        DataContext = ((App)Microsoft.UI.Xaml.Application.Current).ViewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await ((WorkspaceControlViewModel)DataContext).RefreshAsync();

    private void Applications_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SoftwarePage));

    private void Workspace_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(WorkspacePage));

    private void Diagnostics_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(DiagnosticsPage));
}