using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Desktop;

public sealed partial class WorkspacePage : Page
{
    private readonly WorkspaceControlViewModel _viewModel;
    private readonly DispatcherQueueTimer _pollTimer;
    private WorkspaceManifest? _selectedWorkspace;

    public WorkspacePage()
    {
        InitializeComponent();
        _viewModel = ((App)Microsoft.UI.Xaml.Application.Current).ViewModel;
        DataContext = _viewModel;

        _pollTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer()
            ?? throw new InvalidOperationException("The Workspace page must be created on the UI thread.");
        _pollTimer.Interval = TimeSpan.FromMilliseconds(750);
        _pollTimer.Tick += PollTimer_Tick;

        Loaded += WorkspacePage_Loaded;
        Unloaded += WorkspacePage_Unloaded;
    }

    private void WorkspacePage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedWorkspaceId is not null)
        {
            WorkspaceList.SelectedItem = _viewModel.Workspaces.FirstOrDefault(profile =>
                string.Equals(
                    profile.Id,
                    _viewModel.SelectedWorkspaceId,
                    StringComparison.OrdinalIgnoreCase));
        }

        ApplicationSearchBox.Text = _viewModel.ApplicationSearchText;

        if (_viewModel.IsWorkspaceOperationActive)
            _pollTimer.Start();

        UpdateButtons();
    }

    private void WorkspacePage_Unloaded(object sender, RoutedEventArgs e) =>
        _pollTimer.Stop();

    private void WorkspaceList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _selectedWorkspace = WorkspaceList.SelectedItem as WorkspaceManifest;
        if (_selectedWorkspace is not null)
            _viewModel.SelectWorkspace(_selectedWorkspace.Id);

        UpdateButtons();
    }

    private void NewWorkspaceButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CreateBlankWorkspace();
        WorkspaceList.SelectedItem = _viewModel.Workspaces.LastOrDefault();
        UpdateButtons();
    }

    private void SaveWorkspaceButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveWorkspace();

        WorkspaceList.SelectedItem = _viewModel.Workspaces.FirstOrDefault(profile =>
            string.Equals(
                profile.Id,
                _viewModel.SelectedWorkspaceId,
                StringComparison.OrdinalIgnoreCase));

        UpdateButtons();
    }

    private void ApplicationSearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e) =>
        _viewModel.SetApplicationSearchText(ApplicationSearchBox.Text);

    private async void ObserveDiffButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace is null)
            return;

        ObserveDiffButton.IsEnabled = false;
        await _viewModel.ObserveDesiredStateAsync(_selectedWorkspace.Id);
        UpdateButtons();
    }

    private async void CreatePlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace is null)
            return;

        CreatePlanButton.IsEnabled = false;
        await _viewModel.CreateWorkspaceOperationAsync(_selectedWorkspace.Id);
        UpdateButtons();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfirmWorkspaceOperation();
        UpdatePolling();
    }

    private void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResumeWorkspaceOperation();
        UpdatePolling();
    }

    private void PollTimer_Tick(
        DispatcherQueueTimer sender,
        object args)
    {
        _viewModel.RefreshWorkspaceOperation();
        UpdatePolling();
    }

    private void UpdatePolling()
    {
        if (_viewModel.IsWorkspaceOperationActive)
            _pollTimer.Start();
        else
            _pollTimer.Stop();

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var canInteract =
            _selectedWorkspace is not null &&
            !_viewModel.IsBusy &&
            !_viewModel.IsWorkspaceOperationActive;

        ObserveDiffButton.IsEnabled = canInteract;
        CreatePlanButton.IsEnabled =
            canInteract &&
            _viewModel.HasDesiredStateDiffFor(_selectedWorkspace?.Id ?? string.Empty);
    }
}
