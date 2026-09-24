using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorkspaceControl.Application;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Desktop;

public sealed class WorkspaceControlViewModel : INotifyPropertyChanged
{
    private readonly IWorkspaceControlApplication _application;
    private readonly IUiLocalizer _localizer;
    private BaselineSnapshot? _baseline;
    private SoftwareInventorySnapshot? _software;
    private ProvisioningOperation? _provisioningOperation;
    private DesiredStateDiff? _desiredStateDiff;
    private string? _desiredStateDiffProfileId;
    private string? _selectedWorkspaceId;
    private string _workspaceName = string.Empty;
    private string _workspaceDescription = string.Empty;
    private string _applicationSearchText = string.Empty;
    private bool _isBusy;
    private string _status;
    private string? _error;

    public WorkspaceControlViewModel(
        IWorkspaceControlApplication application,
        IUiLocalizer localizer)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _status = _localizer.Get("Ready");
    }

    public BaselineSnapshot? Baseline
    {
        get => _baseline;
        private set => SetField(ref _baseline, value);
    }

    public SoftwareInventorySnapshot? Software
    {
        get => _software;
        private set => SetField(ref _software, value);
    }

    public ProvisioningOperation? ProvisioningOperation
    {
        get => _provisioningOperation;
        private set
        {
            if (!SetField(ref _provisioningOperation, value))
                return;

            OnPropertyChanged(nameof(ProvisioningPlan));
            OnPropertyChanged(nameof(HasProvisioningOperation));
            OnPropertyChanged(nameof(IsAwaitingConfirmation));
            OnPropertyChanged(nameof(IsProvisioningActive));
            OnPropertyChanged(nameof(IsProvisioningCompleted));
            OnPropertyChanged(nameof(IsProvisioningFailed));
            OnPropertyChanged(nameof(IsProvisioningStale));
            OnPropertyChanged(nameof(CanResumeProvisioning));
            OnPropertyChanged(nameof(ProvisioningStatusDisplay));
            OnPropertyChanged(nameof(ProvisioningProgressDisplay));
            OnPropertyChanged(nameof(CurrentComponentDisplay));
            OnPropertyChanged(nameof(ProvisioningError));
            OnPropertyChanged(nameof(HasProvisioningError));
        }
    }

    public DesiredStateDiff? DesiredStateDiff => _desiredStateDiff;
    public bool HasDesiredStateDiff => DesiredStateDiff is not null;
    public ProvisioningPlan? ProvisioningPlan => ProvisioningOperation?.Plan;
    public bool HasProvisioningOperation => ProvisioningOperation is not null;
    public bool IsAwaitingConfirmation =>
        ProvisioningOperation?.Status == ProvisioningOperationStatuses.AwaitingConfirmation;
    public bool IsProvisioningActive =>
        ProvisioningOperation?.Status is ProvisioningOperationStatuses.Queued
            or ProvisioningOperationStatuses.Running;
    public bool IsProvisioningCompleted =>
        ProvisioningOperation?.Status == ProvisioningOperationStatuses.Completed;
    public bool IsProvisioningFailed =>
        ProvisioningOperation?.Status == ProvisioningOperationStatuses.Failed;
    public bool IsProvisioningStale =>
        ProvisioningOperation?.Status == ProvisioningOperationStatuses.Stale;
    public bool CanResumeProvisioning =>
        ProvisioningOperation?.Status == ProvisioningOperationStatuses.Failed
        && ProvisioningOperation.CanResume;

    public string ProvisioningStatusDisplay =>
        ProvisioningOperation?.Status switch
        {
            ProvisioningOperationStatuses.AwaitingConfirmation => _localizer.Get("AwaitingConfirmation"),
            ProvisioningOperationStatuses.Queued => _localizer.Get("Queued"),
            ProvisioningOperationStatuses.Running => _localizer.Get("ApplyingConfirmedPlan"),
            ProvisioningOperationStatuses.Completed => _localizer.Get("Completed"),
            ProvisioningOperationStatuses.Failed => _localizer.Get("Failed"),
            ProvisioningOperationStatuses.Stale => _localizer.Get("PlanIsStale"),
            _ => _localizer.Get("NoProvisioningOperation")
        };

    public string ProvisioningProgressDisplay =>
        ProvisioningOperation is null
            ? _localizer.Get("NoOperation")
            : _localizer.Format(
                "StepsCompletedFormat",
                ProvisioningOperation.Completed,
                ProvisioningOperation.Total);

    public string CurrentComponentDisplay =>
        string.IsNullOrWhiteSpace(ProvisioningOperation?.CurrentComponentName)
            ? _localizer.Get("Preparing")
            : ProvisioningOperation.CurrentComponentName;

    public string? ProvisioningError => ProvisioningOperation?.Error;
    public bool HasProvisioningError => !string.IsNullOrWhiteSpace(ProvisioningError);

    public string MemoryDisplay => Baseline is null ? "—" : $"{Baseline.MemoryGB:F1} GB RAM";
    public string CpuCoresDisplay => Baseline is null ? "—" : $"{Baseline.CpuCores} logical cores";
    public string UptimeDisplay => Baseline is null ? "—" : $"{Baseline.UptimeHours:F1} hours uptime";
    public string SoftwareCountDisplay => $"{SoftwareItems.Count} detected entries";
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public ObservableCollection<SoftwareItem> SoftwareItems { get; } = [];
    public ObservableCollection<string> Diagnostics { get; } = [];
    public ObservableCollection<ProfileManifest> Profiles { get; } = [];
    public ObservableCollection<WorkspaceApplicationOption> ApplicationOptions { get; } = [];
    public ObservableCollection<DesiredStateDiffItem> DesiredStateDiffItems { get; } = [];

    public string? SelectedWorkspaceId
    {
        get => _selectedWorkspaceId;
        private set => SetField(ref _selectedWorkspaceId, value);
    }

    public string WorkspaceName
    {
        get => _workspaceName;
        set => SetField(ref _workspaceName, value);
    }

    public string WorkspaceDescription
    {
        get => _workspaceDescription;
        set => SetField(ref _workspaceDescription, value);
    }

    public string ApplicationSearchText
    {
        get => _applicationSearchText;
        private set => SetField(ref _applicationSearchText, value);
    }

    public bool HasSelectedWorkspace => !string.IsNullOrWhiteSpace(SelectedWorkspaceId);
    public bool IsSelectedWorkspaceUserOwned =>
        SelectedWorkspaceId?.StartsWith("workspace-", StringComparison.OrdinalIgnoreCase) == true;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (!SetField(ref _error, value))
                return;

            OnPropertyChanged(nameof(HasError));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        Status = _localizer.Get("InspectingPc");

        try
        {
            Baseline = _application.GetBaseline();
            OnPropertyChanged(nameof(MemoryDisplay));
            OnPropertyChanged(nameof(CpuCoresDisplay));
            OnPropertyChanged(nameof(UptimeDisplay));

            ClearDesiredStateDiff();

            Profiles.Clear();
            foreach (var profile in _application.GetProfiles())
                Profiles.Add(profile);

            var catalog = _application.GetApplicationCatalog();
            ApplicationOptions.Clear();
            foreach (var component in catalog)
            {
                ApplicationOptions.Add(new WorkspaceApplicationOption(
                    component.Id,
                    component.Name,
                    component.Source));
            }

            var selected = Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase))
                ?? Profiles.FirstOrDefault(profile =>
                    profile.Id.StartsWith("workspace-", StringComparison.OrdinalIgnoreCase));

            if (selected is not null)
                SelectWorkspace(selected.Id);
            else
                ClearWorkspaceEditor();

            var recovery = _application.GetProvisioningRecovery();
            SetProvisioningOperation(recovery);

            var software = await _application.GetSoftwareInventoryAsync(cancellationToken);
            Software = software;

            SoftwareItems.Clear();
            foreach (var item in software.Items)
                SoftwareItems.Add(item);
            OnPropertyChanged(nameof(SoftwareCountDisplay));

            Diagnostics.Clear();
            foreach (var diagnostic in software.Diagnostics)
                Diagnostics.Add(diagnostic);

            Status = _localizer.Format("ReadySoftwareEntriesFormat", SoftwareItems.Count);
        }
        catch (OperationCanceledException)
        {
            Status = _localizer.Get("RefreshCancelled");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Diagnostics.Add(ex.Message);
            Status = _localizer.Get("RefreshFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ObserveDesiredStateAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        Status = _localizer.Get("ObservingDesiredState");

        try
        {
            var diff = await _application.GetDesiredStateDiffAsync(profileId, cancellationToken);
            _desiredStateDiff = diff;
            _desiredStateDiffProfileId = profileId;

            DesiredStateDiffItems.Clear();
            foreach (var item in diff.Items)
                DesiredStateDiffItems.Add(item);

            OnPropertyChanged(nameof(DesiredStateDiff));
            OnPropertyChanged(nameof(HasDesiredStateDiff));
            Status = _localizer.Get("DesiredStateDiffReady");
        }
        catch (OperationCanceledException)
        {
            ClearDesiredStateDiff();
            Status = _localizer.Get("DesiredStateObservationCancelled");
        }
        catch (Exception ex)
        {
            ClearDesiredStateDiff();
            Error = ex.Message;
            Status = _localizer.Get("DesiredStateObservationFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool HasDesiredStateDiffFor(string profileId) =>
        HasDesiredStateDiff &&
        string.Equals(_desiredStateDiffProfileId, profileId, StringComparison.OrdinalIgnoreCase);

    public void ClearDesiredStateDiff()
    {
        _desiredStateDiff = null;
        _desiredStateDiffProfileId = null;
        DesiredStateDiffItems.Clear();
        OnPropertyChanged(nameof(DesiredStateDiff));
        OnPropertyChanged(nameof(HasDesiredStateDiff));
    }

    public void SelectWorkspace(string workspaceId)
    {
        var workspace = Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, workspaceId, StringComparison.OrdinalIgnoreCase));

        if (workspace is null)
            return;

        SelectedWorkspaceId = workspace.Id;
        WorkspaceName = workspace.Name;
        WorkspaceDescription = workspace.Description;

        var selectedApplications = workspace.ApplicationRequests
            .Where(application => string.Equals(application.State, "present", StringComparison.OrdinalIgnoreCase))
            .Select(application => application.ComponentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var option in ApplicationOptions)
            option.IsSelected = selectedApplications.Contains(option.ComponentId);

        ClearDesiredStateDiff();
        OnPropertyChanged(nameof(HasSelectedWorkspace));
        OnPropertyChanged(nameof(IsSelectedWorkspaceUserOwned));
    }

    public void ClearWorkspaceEditor()
    {
        SelectedWorkspaceId = null;
        WorkspaceName = _localizer.Get("DefaultWorkspaceName");
        WorkspaceDescription = _localizer.Get("DefaultWorkspaceDescription");

        foreach (var option in ApplicationOptions)
            option.IsSelected = false;

        ClearDesiredStateDiff();
        OnPropertyChanged(nameof(HasSelectedWorkspace));
        OnPropertyChanged(nameof(IsSelectedWorkspaceUserOwned));
    }

    public void SetApplicationSearchText(string value)
    {
        ApplicationSearchText = value ?? string.Empty;
        foreach (var option in ApplicationOptions)
            option.IsVisible = string.IsNullOrWhiteSpace(ApplicationSearchText)
                || option.Name.Contains(ApplicationSearchText, StringComparison.OrdinalIgnoreCase)
                || option.ComponentId.Contains(ApplicationSearchText, StringComparison.OrdinalIgnoreCase)
                || option.Source?.Contains(ApplicationSearchText, StringComparison.OrdinalIgnoreCase) == true;
    }

    public void CreateBlankWorkspace()
    {
        try
        {
            var workspace = _application.CreateWorkspace(
                _localizer.Get("DefaultWorkspaceName"),
                _localizer.Get("DefaultWorkspaceDescription"),
                []);

            Profiles.Add(workspace);
            SelectWorkspace(workspace.Id);
            Status = _localizer.Get("WorkspaceCreated");
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = _localizer.Get("WorkspaceCreationFailed");
        }
    }

    public void SaveWorkspace()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceName))
        {
            Error = _localizer.Get("WorkspaceNameRequired");
            return;
        }

        var selectedApplications = ApplicationOptions
            .Where(option => option.IsSelected)
            .Select(option => option.ComponentId)
            .ToArray();

        try
        {
            if (IsSelectedWorkspaceUserOwned && SelectedWorkspaceId is not null)
            {
                var current = Profiles.First(profile =>
                    string.Equals(profile.Id, SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase));

                var desiredState = current.DesiredState
                    ?? new DesiredStateManifest([], [], [], [], [], []);

                var updated = current with
                {
                    Name = WorkspaceName.Trim(),
                    Description = WorkspaceDescription.Trim(),
                    SchemaVersion = 2,
                    DesiredState = desiredState with
                    {
                        Applications = selectedApplications
                            .Select(componentId => new ProfileApplication(componentId))
                            .ToArray()
                    }
                };

                _application.SaveWorkspace(updated);

                var index = Profiles.IndexOf(current);
                Profiles[index] = updated;
                SelectWorkspace(updated.Id);
                Status = _localizer.Get("WorkspaceSaved");
                Error = null;
                return;
            }

            var created = _application.CreateWorkspace(
                WorkspaceName.Trim(),
                WorkspaceDescription.Trim(),
                selectedApplications);

            Profiles.Add(created);
            SelectWorkspace(created.Id);
            Status = _localizer.Get("WorkspaceCreatedFromTemplate");
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = _localizer.Get("WorkspaceSaveFailed");
        }
    }

    public async Task CreateProvisioningAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        Status = _localizer.Get("GeneratingProvisioningPlan");

        try
        {
            SetProvisioningOperation(
                await _application.CreateProvisioningAsync(profileId, cancellationToken));
            Status = _localizer.Get("PlanReadyForReview");
        }
        catch (OperationCanceledException)
        {
            Status = _localizer.Get("PlanGenerationCancelled");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = _localizer.Get("PlanGenerationFailed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ConfirmProvisioning()
    {
        if (ProvisioningOperation is null)
            return;

        Error = null;
        Status = _localizer.Get("StartingProvisioning");

        try
        {
            _application.ConfirmProvisioning(ProvisioningOperation.OperationId);
            RefreshProvisioningOperation();
            Status = _localizer.Get("ProvisioningStarted");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            RefreshProvisioningOperation();
            Status = _localizer.Get("ProvisioningCouldNotStart");
        }
    }

    public void ResumeProvisioning()
    {
        if (ProvisioningOperation is null)
            return;

        Error = null;
        Status = _localizer.Get("ResumingProvisioning");

        try
        {
            _application.ResumeProvisioning(ProvisioningOperation.OperationId);
            RefreshProvisioningOperation();
            Status = _localizer.Get("ProvisioningResumed");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            RefreshProvisioningOperation();
            Status = _localizer.Get("ProvisioningCouldNotResume");
        }
    }

    public bool RefreshProvisioningOperation()
    {
        if (ProvisioningOperation is null)
            return false;

        var operation = _application.GetProvisioningStatus(ProvisioningOperation.OperationId);
        SetProvisioningOperation(operation);
        return operation is not null;
    }

    private void SetProvisioningOperation(ProvisioningOperation? operation) =>
        ProvisioningOperation = operation;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}


public sealed class WorkspaceApplicationOption : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isVisible = true;

    public WorkspaceApplicationOption(string componentId, string name, string? source)
    {
        ComponentId = componentId;
        Name = name;
        Source = source;
    }

    public string ComponentId { get; }
    public string Name { get; }
    public string? Source { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;

            _isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
