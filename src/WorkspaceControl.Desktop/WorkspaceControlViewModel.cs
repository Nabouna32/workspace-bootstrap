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

            Profiles.Clear();
            foreach (var profile in _application.GetProfiles())
                Profiles.Add(profile);

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
