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

    private void ApplicationSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ((WorkspaceControlViewModel)DataContext).SetApplicationCatalogSearchText(ApplicationSearchBox.Text);

    private void ApplicationFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ApplicationFilterBox.SelectedItem is ComboBoxItem item && item.Tag is string filter)
            ((WorkspaceControlViewModel)DataContext).SetApplicationCatalogFilter(filter);
    }

    private void AddToWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string componentId })
            return;

        var viewModel = (WorkspaceControlViewModel)DataContext;
        viewModel.AddApplicationToWorkspace(componentId);

        Frame.Navigate(typeof(ProfilesPage));
    }
}
