using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkspaceControl.Desktop;

public sealed partial class SoftwarePage : Page
{
    public SoftwarePage()
    {
        InitializeComponent();
        DataContext = ((App)Microsoft.UI.Xaml.Application.Current).ViewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await ((WorkspaceControlViewModel)DataContext).RefreshAsync();
}
