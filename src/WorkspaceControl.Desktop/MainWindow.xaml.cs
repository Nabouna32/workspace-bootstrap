using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;

namespace WorkspaceControl.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootGrid.DataContext = ((App)Microsoft.UI.Xaml.Application.Current).ViewModel;
        Activated += MainWindow_Activated;
        TryApplyMicaBackdrop();
    }

    private void TryApplyMicaBackdrop()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Mica backdrop is unavailable: {exception}");
        }
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;

        // WinUI NavigationView has a startup-time native XAML regression when SelectedItem is assigned
        // from the constructor. Defer the initial selection until the window has been activated.
        NavView.SelectedItem = NavView.MenuItems[0];

        try
        {
            await ((App)Microsoft.UI.Xaml.Application.Current).ViewModel.RefreshAsync();
            Navigate("overview");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
            Navigate(tag);
    }

    private void Navigate(string tag)
    {
        var pageType = tag switch
        {
            "overview" => typeof(OverviewPage),
            "software" => typeof(SoftwarePage),
            "windows" => typeof(WindowsPage),
            "diagnostics" => typeof(DiagnosticsPage),
            "profiles" => typeof(ProfilesPage),
            _ => typeof(OverviewPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    private void ContentFrame_NavigationFailed(
        object sender,
        NavigationFailedEventArgs e) =>
        ShowError(e.Exception);

    private void ShowError(Exception exception)
    {
        StatusInfoBar.Title = "Workspace Control";
        StatusInfoBar.Message = exception.Message;
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.IsOpen = true;
    }
}
