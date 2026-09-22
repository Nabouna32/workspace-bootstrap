using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorkspaceBootstrap;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Desktop;

public sealed class WorkspaceControlViewModel : INotifyPropertyChanged
{
    private readonly EngineFacade _engine;
    private BaselineSnapshot? _baseline;
    private SoftwareInventorySnapshot? _software;
    private bool _isBusy;
    private string _status = "Ready";
    private string? _error;

    public WorkspaceControlViewModel(EngineFacade engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

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

    public string MemoryDisplay => Baseline is null ? "—" : $"{Baseline.MemoryGB:F1} GB RAM";
    public string CpuCoresDisplay => Baseline is null ? "—" : $"{Baseline.CpuCores} logical cores";
    public string UptimeDisplay => Baseline is null ? "—" : $"{Baseline.UptimeHours:F1} hours uptime";

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
        private set => SetField(ref _error, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        Status = "Inspecting this PC…";

        try
        {
            Baseline = _engine.GetBaseline();
            OnPropertyChanged(nameof(MemoryDisplay));
            OnPropertyChanged(nameof(CpuCoresDisplay));
            OnPropertyChanged(nameof(UptimeDisplay));
            Profiles.Clear();
            foreach (var profile in _engine.GetProfiles())
                Profiles.Add(profile);

            var software = await _engine.GetSoftwareInventoryAsync(cancellationToken);
            Software = software;

            SoftwareItems.Clear();
            foreach (var item in software.Items)
                SoftwareItems.Add(item);

            Diagnostics.Clear();
            foreach (var diagnostic in software.Diagnostics)
                Diagnostics.Add(diagnostic);

            Status = $"Ready · {SoftwareItems.Count} software entries";
        }
        catch (OperationCanceledException)
        {
            Status = "Refresh cancelled";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Diagnostics.Add(ex.Message);
            Status = "Refresh failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
