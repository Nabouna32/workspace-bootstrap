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
    private WorkspaceOperation? _workspaceOperation;
    private DesiredStateDiff? _desiredStateDiff;
    private string? _desiredStateDiffWorkspaceId;
    private string? _selectedWorkspaceId;
    private string _workspaceName = string.Empty;
    private string _workspaceDescription = string.Empty;
    private string _windowsAppearance = WindowsAppearanceValues.Undefined;
    private string _applicationSearchText = string.Empty;
    private string _applicationCatalogSearchText = string.Empty;
    private string _applicationCatalogFilter = ApplicationCatalogFilters.All;
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
        private set
        {
            if (!SetField(ref _software, value))
                return;

            OnPropertyChanged(nameof(HasSoftwareObservation));
        }
    }

    public bool HasSoftwareObservation => Software is not null;

    public WorkspaceOperation? WorkspaceOperation
    {
        get => _workspaceOperation;
        private set
        {
            if (!SetField(ref _workspaceOperation, value))
                return;

            OnPropertyChanged(nameof(WorkspacePlan));
            OnPropertyChanged(nameof(HasWorkspaceOperation));
            OnPropertyChanged(nameof(IsAwaitingConfirmation));
            OnPropertyChanged(nameof(IsWorkspaceOperationActive));
            OnPropertyChanged(nameof(IsWorkspaceOperationCompleted));
            OnPropertyChanged(nameof(IsWorkspaceOperationFailed));
            OnPropertyChanged(nameof(IsWorkspaceOperationStale));
            OnPropertyChanged(nameof(CanResumeWorkspaceOperation));
            OnPropertyChanged(nameof(WorkspaceOperationStatusDisplay));
            OnPropertyChanged(nameof(WorkspaceOperationProgressDisplay));
            OnPropertyChanged(nameof(CurrentComponentDisplay));
            OnPropertyChanged(nameof(WorkspaceOperationError));
            OnPropertyChanged(nameof(HasWorkspaceOperationError));
        }
    }

    public DesiredStateDiff? DesiredStateDiff => _desiredStateDiff;
    public bool HasDesiredStateDiff => DesiredStateDiff is not null;
    public WorkspacePlan? WorkspacePlan => WorkspaceOperation?.Plan;
    public bool HasWorkspaceOperation => WorkspaceOperation is not null;
    public bool IsAwaitingConfirmation =>
        WorkspaceOperation?.Status == WorkspaceOperationStatuses.AwaitingConfirmation;
    public bool IsWorkspaceOperationActive =>
        WorkspaceOperation?.Status is WorkspaceOperationStatuses.Queued
            or WorkspaceOperationStatuses.Running;
    public bool IsWorkspaceOperationCompleted =>
        WorkspaceOperation?.Status == WorkspaceOperationStatuses.Completed;
    public bool IsWorkspaceOperationFailed =>
        WorkspaceOperation?.Status == WorkspaceOperationStatuses.Failed;
    public bool IsWorkspaceOperationStale =>
        WorkspaceOperation?.Status == WorkspaceOperationStatuses.Stale;
    public bool CanResumeWorkspaceOperation =>
        WorkspaceOperation?.Status == WorkspaceOperationStatuses.Failed
        && WorkspaceOperation.CanResume;

    public string WorkspaceOperationStatusDisplay =>
        WorkspaceOperation?.Status switch
        {
            WorkspaceOperationStatuses.AwaitingConfirmation => _localizer.Get("AwaitingConfirmation"),
            WorkspaceOperationStatuses.Queued => _localizer.Get("Queued"),
            WorkspaceOperationStatuses.Running => _localizer.Get("ApplyingConfirmedPlan"),
            WorkspaceOperationStatuses.Completed => _localizer.Get("Completed"),
            WorkspaceOperationStatuses.Failed => _localizer.Get("Failed"),
            WorkspaceOperationStatuses.Stale => _localizer.Get("PlanIsStale"),
            _ => _localizer.Get("NoWorkspaceOperation")
        };

    public string WorkspaceOperationProgressDisplay =>
        WorkspaceOperation is null
            ? _localizer.Get("NoOperation")
            : _localizer.Format(
                "StepsCompletedFormat",
                WorkspaceOperation.Completed,
                WorkspaceOperation.Total);

    public string CurrentComponentDisplay =>
        string.IsNullOrWhiteSpace(WorkspaceOperation?.CurrentComponentName)
            ? _localizer.Get("Preparing")
            : WorkspaceOperation.CurrentComponentName;

    public string? WorkspaceOperationError => WorkspaceOperation?.Error;
    public bool HasWorkspaceOperationError => !string.IsNullOrWhiteSpace(WorkspaceOperationError);

    public string MemoryDisplay => Baseline is null ? "—" : $"{Baseline.MemoryGB:F1} GB RAM";
    public string CpuCoresDisplay => Baseline is null ? "—" : $"{Baseline.CpuCores} logical cores";
    public string UptimeDisplay => Baseline is null ? "—" : $"{Baseline.UptimeHours:F1} hours uptime";
    public string SoftwareCountDisplay => _localizer.Format("SoftwareCountFormat", SoftwareItems.Count);
    public int ApplicationInstalledCount => ApplicationCatalogItems.Count(item => item.IsInstalled);
    public int ApplicationUpdatesCount => ApplicationCatalogItems.Count(item => item.IsUpdateAvailable);
    public int ApplicationAvailableCount => ApplicationCatalogItems.Count(item => string.Equals(item.StateCode, ApplicationStateCodes.Missing, StringComparison.OrdinalIgnoreCase));
    public int DiagnosticCount => Diagnostics.Count;
    public string HomeHealthCode
    {
        get
        {
            if (HasError || IsWorkspaceOperationFailed || IsWorkspaceOperationStale)
                return "ERROR";

            if (DiagnosticCount > 0 || ApplicationUpdatesCount > 0)
                return "WARNING";

            return "HEALTHY";
        }
    }
    public string HomeHealthDisplay => HomeHealthCode switch
    {
        "ERROR" => _localizer.Get("HomeHealthError"),
        "WARNING" => _localizer.Get("HomeHealthAttention"),
        _ => _localizer.Get("HomeHealthHealthy")
    };
    public string HomeWorkspaceDisplay => HasSelectedWorkspace
        ? WorkspaceName
        : _localizer.Get("HomeNoWorkspace");
    public string HomeWorkspaceOperationDisplay => WorkspaceOperation is null
        ? _localizer.Get("NoWorkspaceOperation")
        : WorkspaceOperationStatusDisplay;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public ObservableCollection<SoftwareItem> SoftwareItems { get; } = [];
    public ObservableCollection<ApplicationCatalogItem> ApplicationCatalogItems { get; } = [];
    public ObservableCollection<ApplicationCatalogItem> VisibleApplicationCatalogItems { get; } = [];
    public ObservableCollection<string> Diagnostics { get; } = [];
    public ObservableCollection<WorkspaceManifest> Workspaces { get; } = [];
    public ObservableCollection<WorkspaceApplicationOption> ApplicationOptions { get; } = [];
    public ObservableCollection<WorkspaceApplicationOption> VisibleApplicationOptions { get; } = [];
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

    public string WindowsAppearance
    {
        get => _windowsAppearance;
        set
        {
            if (!SetField(ref _windowsAppearance, value))
                return;

            OnPropertyChanged(nameof(WindowsAppearanceDisplay));
        }
    }

    public string WindowsAppearanceDisplay => WindowsAppearance switch
    {
        WindowsAppearanceValues.Light => _localizer.Get("WindowsAppearanceLight"),
        WindowsAppearanceValues.Dark => _localizer.Get("WindowsAppearanceDark"),
        WindowsAppearanceValues.System => _localizer.Get("WindowsAppearanceSystem"),
        _ => _localizer.Get("Undefined")
    };

    public string ApplicationSearchText
    {
        get => _applicationSearchText;
        private set => SetField(ref _applicationSearchText, value);
    }

    public string ApplicationCatalogSearchText
    {
        get => _applicationCatalogSearchText;
        private set => SetField(ref _applicationCatalogSearchText, value);
    }

    public string ApplicationCatalogFilter
    {
        get => _applicationCatalogFilter;
        private set => SetField(ref _applicationCatalogFilter, value);
    }

    public bool HasSelectedWorkspace => !string.IsNullOrWhiteSpace(SelectedWorkspaceId);
    public bool IsSelectedWorkspaceUserOwned =>
        SelectedWorkspaceId is not null
        && Workspaces.Any(workspace =>
            string.Equals(workspace.Id, SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase));

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
            NotifyHomeDashboardChanged();
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

            Workspaces.Clear();
            foreach (var workspace in _application.GetWorkspaces())
                Workspaces.Add(workspace);

            var catalog = _application.GetApplicationCatalog();
            ApplicationOptions.Clear();
            foreach (var component in catalog)
            {
                ApplicationOptions.Add(new WorkspaceApplicationOption(
                    component.Id,
                    component.Name,
                    component.Source));
            }

            SetApplicationSearchText(string.Empty);

            var selected = Workspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id, SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase))
                ?? Workspaces.FirstOrDefault();

            if (selected is not null)
                SelectWorkspace(selected.Id);
            else
                ClearWorkspaceEditor();

            var recovery = _application.GetRecoverableWorkspaceOperation();
            SetWorkspaceOperation(recovery);

            var software = await _application.GetSoftwareInventoryAsync(cancellationToken);
            Software = software;

            SoftwareItems.Clear();
            foreach (var item in software.Items)
                SoftwareItems.Add(item);
            OnPropertyChanged(nameof(SoftwareCountDisplay));

            RebuildApplicationCatalog(catalog, software);

            Diagnostics.Clear();
            foreach (var diagnostic in software.Diagnostics)
                Diagnostics.Add(diagnostic);

            NotifyHomeDashboardChanged();
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
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        Status = _localizer.Get("ObservingDesiredState");

        try
        {
            var diff = await _application.GetDesiredStateDiffAsync(workspaceId, cancellationToken);
            _desiredStateDiff = diff;
            _desiredStateDiffWorkspaceId = workspaceId;

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

    public bool HasDesiredStateDiffFor(string workspaceId) =>
        HasDesiredStateDiff &&
        string.Equals(_desiredStateDiffWorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase);

    public void ClearDesiredStateDiff()
    {
        _desiredStateDiff = null;
        _desiredStateDiffWorkspaceId = null;
        DesiredStateDiffItems.Clear();
        OnPropertyChanged(nameof(DesiredStateDiff));
        OnPropertyChanged(nameof(HasDesiredStateDiff));
    }

    public void SelectWorkspace(string workspaceId)
    {
        var workspace = Workspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id, workspaceId, StringComparison.OrdinalIgnoreCase));

        if (workspace is null)
            return;

        SelectedWorkspaceId = workspace.Id;
        WorkspaceName = workspace.Name;
        WorkspaceDescription = workspace.Description;

        var applicationIntents = workspace.ApplicationRequests
            .ToDictionary(application => application.ComponentId, StringComparer.OrdinalIgnoreCase);

        foreach (var option in ApplicationOptions)
        {
            option.Intent = applicationIntents.TryGetValue(option.ComponentId, out var application)
                ? application.State
                : WorkspaceApplicationIntentCodes.Undefined;
        }

        WindowsAppearance = WindowsAppearanceSettings.Read(
            workspace.DesiredState?.RegistrySettings ?? []);

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
            option.Intent = WorkspaceApplicationIntentCodes.Undefined;

        WindowsAppearance = WindowsAppearanceValues.Undefined;

        ClearDesiredStateDiff();
        OnPropertyChanged(nameof(HasSelectedWorkspace));
        OnPropertyChanged(nameof(IsSelectedWorkspaceUserOwned));
    }

    public void SetApplicationCatalogSearchText(string value)
    {
        ApplicationCatalogSearchText = value ?? string.Empty;
        RefreshVisibleApplicationCatalog();
    }

    public void SetApplicationCatalogFilter(string filter)
    {
        ApplicationCatalogFilter = string.IsNullOrWhiteSpace(filter)
            ? ApplicationCatalogFilters.All
            : filter;
        RefreshVisibleApplicationCatalog();
    }

    public void AddApplicationToWorkspace(string componentId)
    {
        if (!IsSelectedWorkspaceUserOwned)
        {
            Error = _localizer.Get("WorkspaceSelectionRequired");
            Status = _localizer.Get("WorkspaceSelectionRequired");
            return;
        }

        var option = ApplicationOptions.FirstOrDefault(item =>
            string.Equals(item.ComponentId, componentId, StringComparison.OrdinalIgnoreCase));

        if (option is null)
            return;

        option.Intent = WorkspaceApplicationIntentCodes.Present;
        SaveWorkspace();
        Status = _localizer.Get("ApplicationAddedToWorkspace");
        Error = null;
    }

    private void RebuildApplicationCatalog(
        IReadOnlyList<ComponentManifest> catalog,
        SoftwareInventorySnapshot software)
    {
        ApplicationCatalogItems.Clear();

        foreach (var component in catalog.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var installed = software.Items.FirstOrDefault(item =>
                (!string.IsNullOrWhiteSpace(component.PackageId)
                    && string.Equals(item.Id, component.PackageId, StringComparison.OrdinalIgnoreCase))
                || string.Equals(item.Name, component.Name, StringComparison.OrdinalIgnoreCase));

            var state = software.Diagnostics.Count > 0 && installed is null
                ? ApplicationStateCodes.Unknown
                : installed is null
                    ? ApplicationStateCodes.Missing
                    : !string.IsNullOrWhiteSpace(installed.AvailableVersion)
                        ? ApplicationStateCodes.Outdated
                        : ApplicationStateCodes.Installed;

            var action = state switch
            {
                ApplicationStateCodes.Missing => DesiredStateActionCodes.Install,
                ApplicationStateCodes.Outdated => DesiredStateActionCodes.Update,
                ApplicationStateCodes.Unknown => DesiredStateActionCodes.Blocked,
                _ => DesiredStateActionCodes.None
            };

            ApplicationCatalogItems.Add(new ApplicationCatalogItem(
                component.Id,
                component.Name,
                component.Source,
                installed?.Version,
                installed?.AvailableVersion,
                state,
                action,
                installed is not null));
            
            var workspaceOption = ApplicationOptions.FirstOrDefault(option =>
                string.Equals(option.ComponentId, component.Id, StringComparison.OrdinalIgnoreCase));
            workspaceOption?.UpdateObservedState(
                state,
                installed?.Version,
                installed?.AvailableVersion);
        }

        RefreshVisibleApplicationCatalog();
    }

    private void RefreshVisibleApplicationCatalog()
    {
        VisibleApplicationCatalogItems.Clear();

        foreach (var item in ApplicationCatalogItems)
        {
            var matchesSearch = string.IsNullOrWhiteSpace(ApplicationCatalogSearchText)
                || item.Name.Contains(ApplicationCatalogSearchText, StringComparison.OrdinalIgnoreCase)
                || item.ComponentId.Contains(ApplicationCatalogSearchText, StringComparison.OrdinalIgnoreCase)
                || item.Source?.Contains(ApplicationCatalogSearchText, StringComparison.OrdinalIgnoreCase) == true;

            var matchesFilter = ApplicationCatalogFilter switch
            {
                ApplicationCatalogFilters.Installed => item.IsInstalled,
                ApplicationCatalogFilters.Updates => item.IsUpdateAvailable,
                ApplicationCatalogFilters.Available => string.Equals(item.StateCode, ApplicationStateCodes.Missing, StringComparison.OrdinalIgnoreCase),
                _ => true
            };

            if (matchesSearch && matchesFilter)
                VisibleApplicationCatalogItems.Add(item);
        }
    }

    public void SetApplicationSearchText(string value)
    {
        ApplicationSearchText = value ?? string.Empty;
        VisibleApplicationOptions.Clear();

        foreach (var option in ApplicationOptions)
        {
            if (string.IsNullOrWhiteSpace(ApplicationSearchText)
                || option.Name.Contains(ApplicationSearchText, StringComparison.OrdinalIgnoreCase)
                || option.ComponentId.Contains(ApplicationSearchText, StringComparison.OrdinalIgnoreCase)
                || option.Source?.Contains(ApplicationSearchText, StringComparison.OrdinalIgnoreCase) == true)
            {
                VisibleApplicationOptions.Add(option);
            }
        }
    }

    public void CaptureCurrentMachineApplications()
    {
        if (Software is null)
        {
            Error = _localizer.Get("WorkspaceCaptureUnavailable");
            Status = _localizer.Get("WorkspaceCaptureUnavailable");
            return;
        }

        try
        {
            var capturedComponentIds = ApplicationCatalogItems
                .Where(item => item.IsInstalled)
                .Select(item => item.ComponentId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var workspace = _application.CreateWorkspace(
                _localizer.Get("CapturedWorkspaceName"),
                _localizer.Get("CapturedWorkspaceDescription"),
                capturedComponentIds);

            Workspaces.Add(workspace);
            SelectWorkspace(workspace.Id);
            Status = _localizer.Format("WorkspaceCaptured", capturedComponentIds.Length);
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = _localizer.Get("WorkspaceCaptureFailed");
        }
    }

    public void CreateBlankWorkspace()
    {
        try
        {
            var workspace = _application.CreateWorkspace(
                _localizer.Get("DefaultWorkspaceName"),
                _localizer.Get("DefaultWorkspaceDescription"),
                []);

            Workspaces.Add(workspace);
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
            .Where(option => !string.Equals(option.Intent, WorkspaceApplicationIntentCodes.Undefined, StringComparison.OrdinalIgnoreCase))
            .Select(option => new WorkspaceApplication(
                option.ComponentId,
                State: option.Intent))
            .ToArray();

        try
        {
            if (IsSelectedWorkspaceUserOwned && SelectedWorkspaceId is not null)
            {
                var current = Workspaces.First(workspace =>
                    string.Equals(workspace.Id, SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase));

                var desiredState = current.DesiredState
                    ?? new DesiredStateManifest([], [], [], [], [], []);

                var updated = current with
                {
                    Name = WorkspaceName.Trim(),
                    Description = WorkspaceDescription.Trim(),
                    SchemaVersion = 2,
                    DesiredState = desiredState with
                    {
                        Applications = selectedApplications,
                        RegistrySettings = WindowsAppearanceSettings.Apply(
                            desiredState.RegistrySettings,
                            WindowsAppearance)
                    }
                };

                _application.SaveWorkspace(updated);

                var index = Workspaces.IndexOf(current);
                Workspaces[index] = updated;
                SelectWorkspace(updated.Id);
                Status = _localizer.Get("WorkspaceSaved");
                Error = null;
                return;
            }

            var created = _application.CreateWorkspace(
                WorkspaceName.Trim(),
                WorkspaceDescription.Trim(),
                selectedApplications
                    .Where(application => string.Equals(application.State, WorkspaceApplicationIntentCodes.Present, StringComparison.OrdinalIgnoreCase))
                    .Select(application => application.ComponentId)
                    .ToArray());

            Workspaces.Add(created);
            SelectWorkspace(created.Id);
            Status = _localizer.Get("WorkspaceCreated");
            Error = null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = _localizer.Get("WorkspaceSaveFailed");
        }
    }

    public async Task CreateWorkspaceOperationAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        Status = _localizer.Get("GeneratingWorkspacePlan");

        try
        {
            SetWorkspaceOperation(
                await _application.CreateWorkspaceOperationAsync(workspaceId, cancellationToken));
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

    public void ConfirmWorkspaceOperation()
    {
        if (WorkspaceOperation is null)
            return;

        Error = null;
        Status = _localizer.Get("StartingWorkspaceOperation");

        try
        {
            _application.ConfirmWorkspaceOperation(WorkspaceOperation.OperationId);
            RefreshWorkspaceOperation();
            Status = _localizer.Get("WorkspaceOperationStarted");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            RefreshWorkspaceOperation();
            Status = _localizer.Get("WorkspaceOperationCouldNotStart");
        }
    }

    public void ResumeWorkspaceOperation()
    {
        if (WorkspaceOperation is null)
            return;

        Error = null;
        Status = _localizer.Get("ResumingWorkspaceOperation");

        try
        {
            _application.ResumeWorkspaceOperation(WorkspaceOperation.OperationId);
            RefreshWorkspaceOperation();
            Status = _localizer.Get("WorkspaceOperationResumed");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            RefreshWorkspaceOperation();
            Status = _localizer.Get("WorkspaceOperationCouldNotResume");
        }
    }

    public bool RefreshWorkspaceOperation()
    {
        if (WorkspaceOperation is null)
            return false;

        var operation = _application.GetWorkspaceOperation(WorkspaceOperation.OperationId);
        SetWorkspaceOperation(operation);
        return operation is not null;
    }

    private void SetWorkspaceOperation(WorkspaceOperation? operation)
    {
        WorkspaceOperation = operation;
        NotifyHomeDashboardChanged();
    }

    private void NotifyHomeDashboardChanged()
    {
        OnPropertyChanged(nameof(ApplicationInstalledCount));
        OnPropertyChanged(nameof(ApplicationUpdatesCount));
        OnPropertyChanged(nameof(ApplicationAvailableCount));
        OnPropertyChanged(nameof(DiagnosticCount));
        OnPropertyChanged(nameof(HomeHealthCode));
        OnPropertyChanged(nameof(HomeHealthDisplay));
        OnPropertyChanged(nameof(HomeWorkspaceDisplay));
        OnPropertyChanged(nameof(HomeWorkspaceOperationDisplay));
    }

    private void OnPropertyChanged(string? propertyName) =>
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
    private string _intent = WorkspaceApplicationIntentCodes.Undefined;
    private string _stateCode = ApplicationStateCodes.Unknown;
    private string? _installedVersion;
    private string? _availableVersion;

    public WorkspaceApplicationOption(string componentId, string name, string? source)
    {
        ComponentId = componentId;
        Name = name;
        Source = source;
    }

    public string ComponentId { get; }
    public string Name { get; }
    public string? Source { get; }

    public string Intent
    {
        get => _intent;
        set
        {
            var normalized = value?.Trim().ToLowerInvariant() ?? WorkspaceApplicationIntentCodes.Undefined;
            if (normalized is not WorkspaceApplicationIntentCodes.Present
                and not WorkspaceApplicationIntentCodes.Absent)
            {
                normalized = WorkspaceApplicationIntentCodes.Undefined;
            }

            SetField(ref _intent, normalized);
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public bool IsSelected
    {
        get => string.Equals(Intent, WorkspaceApplicationIntentCodes.Present, StringComparison.OrdinalIgnoreCase);
        set => Intent = value
            ? WorkspaceApplicationIntentCodes.Present
            : WorkspaceApplicationIntentCodes.Undefined;
    }

    public string? IntentDisplay => Intent switch
    {
        WorkspaceApplicationIntentCodes.Present => "Keep",
        WorkspaceApplicationIntentCodes.Absent => "Remove",
        _ => "Don't manage"
    };

    public string StateCode
    {
        get => _stateCode;
        private set => SetField(ref _stateCode, value);
    }

    public string? InstalledVersion
    {
        get => _installedVersion;
        private set => SetField(ref _installedVersion, value);
    }

    public string? AvailableVersion
    {
        get => _availableVersion;
        private set => SetField(ref _availableVersion, value);
    }

    public bool IsInstalled =>
        string.Equals(StateCode, ApplicationStateCodes.Installed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(StateCode, ApplicationStateCodes.Outdated, StringComparison.OrdinalIgnoreCase);

    public void UpdateObservedState(
        string stateCode,
        string? installedVersion,
        string? availableVersion)
    {
        StateCode = stateCode;
        InstalledVersion = installedVersion;
        AvailableVersion = availableVersion;
        OnPropertyChanged(nameof(IsInstalled));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public static class ApplicationCatalogFilters
{
    public const string All = "all";
    public const string Installed = "installed";
    public const string Updates = "updates";
    public const string Available = "available";
}

public sealed record ApplicationCatalogItem(
    string ComponentId,
    string Name,
    string? Source,
    string? InstalledVersion,
    string? AvailableVersion,
    string StateCode,
    string ActionCode,
    bool IsInstalled)
{
    public bool IsUpdateAvailable =>
        string.Equals(StateCode, ApplicationStateCodes.Outdated, StringComparison.OrdinalIgnoreCase);
}

