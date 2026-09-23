using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Desktop;

public sealed partial class ProfilesPage : Page
{
    private readonly WorkspaceControlViewModel _viewModel;
    private readonly DispatcherQueueTimer _pollTimer;
    private ProfileManifest? _selectedProfile;

    public ProfilesPage()
    {
        InitializeComponent();
        _viewModel = ((App)Microsoft.UI.Xaml.Application.Current).ViewModel;
        DataContext = _viewModel;

        _pollTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer()
            ?? throw new InvalidOperationException("The provisioning page must be created on the UI thread.");
        _pollTimer.Interval = TimeSpan.FromMilliseconds(750);
        _pollTimer.Tick += PollTimer_Tick;

        Loaded += ProfilesPage_Loaded;
        Unloaded += ProfilesPage_Unloaded;
    }

    private void ProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsProvisioningActive)
            _pollTimer.Start();

        UpdateCreatePlanButton();
    }

    private void ProfilesPage_Unloaded(object sender, RoutedEventArgs e) =>
        _pollTimer.Stop();

    private void ProfileList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _selectedProfile = ProfileList.SelectedItem as ProfileManifest;
        _viewModel.ClearDesiredStateDiff();
        UpdateCreatePlanButton();
    }

    private async void ObserveDiffButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile is null)
            return;

        ObserveDiffButton.IsEnabled = false;
        await _viewModel.ObserveDesiredStateAsync(_selectedProfile.Id);
        UpdateCreatePlanButton();
    }

    private async void CreatePlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProfile is null)
            return;

        CreatePlanButton.IsEnabled = false;
        await _viewModel.CreateProvisioningAsync(_selectedProfile.Id);
        UpdateCreatePlanButton();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfirmProvisioning();
        UpdatePolling();
    }

    private void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResumeProvisioning();
        UpdatePolling();
    }

    private void PollTimer_Tick(
        DispatcherQueueTimer sender,
        object args)
    {
        _viewModel.RefreshProvisioningOperation();
        UpdatePolling();
    }

    private void UpdatePolling()
    {
        if (_viewModel.IsProvisioningActive)
            _pollTimer.Start();
        else
            _pollTimer.Stop();

        UpdateCreatePlanButton();
    }

    private void UpdateCreatePlanButton()
    {
        var canInteract =
            _selectedProfile is not null &&
            !_viewModel.IsBusy &&
            !_viewModel.IsProvisioningActive;

        ObserveDiffButton.IsEnabled = canInteract;
        CreatePlanButton.IsEnabled =
            canInteract &&
            _selectedProfile is not null &&
            _viewModel.HasDesiredStateDiffFor(_selectedProfile.Id);
    }
}
