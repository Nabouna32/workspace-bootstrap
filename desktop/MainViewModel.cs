using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorkspaceBootstrap.Desktop;

public enum DesktopPage
{
    Dashboard,
    Optimization,
    Provisioning,
    Diagnostics,
    Inventory
}

public sealed class ProvisioningOperationHistoryItemViewModel
{
    private readonly ProvisioningOperationHistoryItem _item;

    public ProvisioningOperationHistoryItemViewModel(ProvisioningOperationHistoryItem item) => _item = item;

    public string ProfileId => _item.ProfileId;
    public string OperationId => _item.OperationId;
    public string StatusLabel => _item.Status switch
    {
        "completed" => "Terminé",
        "running" => "En cours",
        "starting" => "Démarrage",
        "failed" => "Échec",
        "reboot-required" => "Redémarrage requis",
        _ => _item.Status
    };
    public string ProgressLabel => $"{_item.Percent}% · {_item.Completed}/{_item.Total}";
    public string UpdatedAtLabel => _item.UpdatedAt.ToLocalTime().ToString("g");
    public bool CanResume => _item.CanResume;
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DesktopEngineClient _engineClient;
    private readonly DesktopDialogService _dialogService;
    private readonly ThemeManager _themeManager;
    private InventorySnapshot? _inventorySnapshot;
    private IReadOnlyList<InventoryCleanupRecommendation> _inventoryRecommendations = Array.Empty<InventoryCleanupRecommendation>();
    private bool _isBusy;
    private string _busyMessage = string.Empty;
    private DesktopPage _currentPage = DesktopPage.Dashboard;
    private string _windowsValue = "Analyse…";
    private string _buildValue = string.Empty;
    private string _cpuValue = "Analyse…";
    private string _coreValue = string.Empty;
    private string _memoryValue = "Analyse…";
    private string _uptimeValue = string.Empty;
    private string _storageValue = string.Empty;
    private string _startupValue = string.Empty;
    private string _optimizationOutput = string.Empty;
    private string _provisioningOutput = string.Empty;
    private ProvisioningOperationState? _provisioningOperation;
    private CancellationTokenSource? _provisioningPollingCancellation;
    private string _selectedProvisioningProfileId = "base";
    private string _provisioningProfileDescription = string.Empty;
    private IReadOnlyList<ProvisioningProfileOption> _provisioningProfiles = Array.Empty<ProvisioningProfileOption>();
    private IReadOnlyList<ProvisioningPlanItemViewModel> _provisioningPlanItems = Array.Empty<ProvisioningPlanItemViewModel>();
    private IReadOnlyList<ProvisioningOperationHistoryItemViewModel> _provisioningHistory = Array.Empty<ProvisioningOperationHistoryItemViewModel>();
    private ProvisioningOperationHistoryItemViewModel? _selectedProvisioningHistoryItem;
    private ProvisioningOperationDetail? _provisioningOperationDetail;
    private ThemeOption _selectedTheme = new(ThemeMode.System, "Système");

    public MainViewModel(
        DesktopEngineClient engineClient,
        DesktopDialogService dialogService)
    {
        _engineClient = engineClient;
        _dialogService = dialogService;
        _themeManager = new ThemeManager();
        SelectedTheme = ThemeOptions.First(option => option.Mode == _themeManager.LoadMode());
        DashboardCommand = new AsyncCommand(() => NavigateAsync(DesktopPage.Dashboard));
        OptimizationCommand = new AsyncCommand(() => NavigateAsync(DesktopPage.Optimization));
        ProvisioningCommand = new AsyncCommand(() => NavigateAsync(DesktopPage.Provisioning));
        DiagnosticsCommand = new AsyncCommand(() => NavigateAsync(DesktopPage.Diagnostics));
        InventoryCommand = new AsyncCommand(() => NavigateAsync(DesktopPage.Inventory));
        InventoryScanCommand = new AsyncCommand(ScanInventoryAsync, () => !IsBusy);
        RefreshCommand = new AsyncCommand(RefreshBaselineAsync, () => !IsBusy);
        OptimizationPlanCommand = new AsyncCommand(LoadOptimizationPlanAsync, () => !IsBusy);
        OptimizationApplyCommand = new AsyncCommand(ApplyOptimizationAsync, () => !IsBusy);
        OptimizationRollbackCommand = new AsyncCommand(RollbackOptimizationAsync, () => !IsBusy);
        ProvisioningPlanCommand = new AsyncCommand(LoadProvisioningPlanAsync, () => !IsBusy);
        ProvisioningExecuteCommand = new AsyncCommand(ExecuteProvisioningAsync, () => !IsBusy && !IsProvisioningOperationActive);
        ProvisioningResumeCommand = new AsyncCommand(ResumeProvisioningAsync, () => !IsBusy && ProvisioningOperation?.CanResume == true);
        ProvisioningHistoryCommand = new AsyncCommand(LoadProvisioningHistoryAsync, () => !IsBusy);
        ProvisioningDetailCommand = new AsyncCommand(LoadProvisioningDetailAsync, () => !IsBusy && SelectedProvisioningHistoryItem is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand DashboardCommand { get; }
    public AsyncCommand OptimizationCommand { get; }
    public AsyncCommand ProvisioningCommand { get; }
    public AsyncCommand DiagnosticsCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand OptimizationPlanCommand { get; }
    public AsyncCommand OptimizationApplyCommand { get; }
    public AsyncCommand OptimizationRollbackCommand { get; }
    public AsyncCommand ProvisioningPlanCommand { get; }
    public AsyncCommand ProvisioningExecuteCommand { get; }
    public AsyncCommand ProvisioningResumeCommand { get; }
    public AsyncCommand ProvisioningHistoryCommand { get; }
    public AsyncCommand ProvisioningDetailCommand { get; }
    public AsyncCommand InventoryCommand { get; }
    public AsyncCommand InventoryScanCommand { get; }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(ThemeMode.System, "Système"),
        new(ThemeMode.Light, "Clair"),
        new(ThemeMode.Dark, "Sombre")
    ];

    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!SetProperty(ref _selectedTheme, value)) return;
            _themeManager.Apply(value.Mode);
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        private set => SetProperty(ref _busyMessage, value);
    }

    public DesktopPage CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (!SetProperty(ref _currentPage, value)) return;
            OnPropertyChanged(nameof(IsDashboardVisible));
            OnPropertyChanged(nameof(IsOptimizationVisible));
            OnPropertyChanged(nameof(IsProvisioningVisible));
            OnPropertyChanged(nameof(IsDiagnosticsVisible));
            OnPropertyChanged(nameof(IsInventoryVisible));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageSubtitle));
        }
    }

    public bool IsDashboardVisible => CurrentPage == DesktopPage.Dashboard;
    public bool IsOptimizationVisible => CurrentPage == DesktopPage.Optimization;
    public bool IsProvisioningVisible => CurrentPage == DesktopPage.Provisioning;
    public bool IsDiagnosticsVisible => CurrentPage == DesktopPage.Diagnostics;
    public bool IsInventoryVisible => CurrentPage == DesktopPage.Inventory;

    public string PageTitle => CurrentPage switch
    {
        DesktopPage.Optimization => "Optimisation",
        DesktopPage.Provisioning => "Provisionnement",
        DesktopPage.Diagnostics => "Diagnostic",
        DesktopPage.Inventory => "Logiciels & composants",
        _ => "Tableau de bord"
    };

    public string PageSubtitle => CurrentPage switch
    {
        DesktopPage.Optimization => "Des actions explicites, réversibles et protégées contre les modifications manuelles.",
        DesktopPage.Provisioning => "Installez et maintenez l'environnement depuis une interface graphique sans dupliquer le moteur.",
        DesktopPage.Diagnostics => "Analyse de santé et cohérence de l'environnement.",
        DesktopPage.Inventory => "Inventaire des logiciels connus, versions installées et candidats de nettoyage.",
        _ => "Vue d'ensemble de votre machine et de l'environnement de développement."
    };

    public string WindowsValue { get => _windowsValue; private set => SetProperty(ref _windowsValue, value); }
    public string BuildValue { get => _buildValue; private set => SetProperty(ref _buildValue, value); }
    public string CpuValue { get => _cpuValue; private set => SetProperty(ref _cpuValue, value); }
    public string CoreValue { get => _coreValue; private set => SetProperty(ref _coreValue, value); }
    public string MemoryValue { get => _memoryValue; private set => SetProperty(ref _memoryValue, value); }
    public string UptimeValue { get => _uptimeValue; private set => SetProperty(ref _uptimeValue, value); }
    public string StorageValue { get => _storageValue; private set => SetProperty(ref _storageValue, value); }
    public string StartupValue { get => _startupValue; private set => SetProperty(ref _startupValue, value); }
    public string OptimizationOutput { get => _optimizationOutput; private set => SetProperty(ref _optimizationOutput, value); }
    public string ProvisioningOutput { get => _provisioningOutput; private set => SetProperty(ref _provisioningOutput, value); }
    public ProvisioningOperationState? ProvisioningOperation { get => _provisioningOperation; private set => SetProperty(ref _provisioningOperation, value); }
    public bool IsProvisioningOperationActive => ProvisioningOperation is { Status: "starting" or "running" };
    public bool IsProvisioningOperationVisible => ProvisioningOperation is not null;
    public string ProvisioningOperationStatus => ProvisioningOperation?.MessageKey switch
    {
        "operation.starting" => "Préparation…",
        "component.running" => "Installation en cours…",
        "component.completed" => "Étape terminée",
        "operation.completed" => "Terminé",
        "operation.reboot-required" => "Redémarrage requis",
        "operation.resuming" => "Reprise en cours…",
        "operation.interrupted" => "Opération interrompue",
        "component.failed" or "operation.failed" => "Échec",
        _ => ProvisioningOperation?.Status ?? "Aucune opération"
    };
    public string ProvisioningCurrentStep => ProvisioningOperation is null
        ? string.Empty
        : $"{ProvisioningOperation.Completed}/{ProvisioningOperation.Total} — {ProvisioningOperation.CurrentComponentName ?? ProvisioningOperationStatus}";
    public string SelectedProvisioningProfileId
    {
        get => _selectedProvisioningProfileId;
        set => SetProperty(ref _selectedProvisioningProfileId, value);
    }
    public string ProvisioningProfileDescription { get => _provisioningProfileDescription; private set => SetProperty(ref _provisioningProfileDescription, value); }
    public IReadOnlyList<ProvisioningProfileOption> ProvisioningProfiles { get => _provisioningProfiles; private set => SetProperty(ref _provisioningProfiles, value); }
    public IReadOnlyList<ProvisioningPlanItemViewModel> ProvisioningPlanItems { get => _provisioningPlanItems; private set => SetProperty(ref _provisioningPlanItems, value); }
    public IReadOnlyList<ProvisioningOperationHistoryItemViewModel> ProvisioningHistory { get => _provisioningHistory; private set => SetProperty(ref _provisioningHistory, value); }
    public ProvisioningOperationHistoryItemViewModel? SelectedProvisioningHistoryItem
    {
        get => _selectedProvisioningHistoryItem;
        set
        {
            if (!SetProperty(ref _selectedProvisioningHistoryItem, value)) return;
            ProvisioningOperationDetail = null;
            ProvisioningDetailCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(ProvisioningDetailSummary));
        }
    }
    public ProvisioningOperationDetail? ProvisioningOperationDetail
    {
        get => _provisioningOperationDetail;
        private set
        {
            if (!SetProperty(ref _provisioningOperationDetail, value)) return;
            OnPropertyChanged(nameof(ProvisioningDetailSummary));
            OnPropertyChanged(nameof(ProvisioningDetailLogs));
        }
    }
    public string ProvisioningDetailSummary => ProvisioningOperationDetail?.Summary is null
        ? "Aucun résumé de run disponible."
        : $"{ProvisioningOperationDetail.Summary.Success switch { true => "Succès", false => "Échec" }} · {ProvisioningOperationDetail.Summary.Steps.Count} étape(s) · code {ProvisioningOperationDetail.Summary.ExitCode}";
    public string ProvisioningDetailLogs => ProvisioningOperationDetail?.Logs.Count > 0
        ? string.Join(Environment.NewLine, ProvisioningOperationDetail.Logs)
        : "Aucun fichier de log disponible.";
    public InventorySnapshot? InventorySnapshot { get => _inventorySnapshot; private set => SetProperty(ref _inventorySnapshot, value); }
    public IReadOnlyList<InventoryCleanupRecommendation> InventoryRecommendations { get => _inventoryRecommendations; private set => SetProperty(ref _inventoryRecommendations, value); }
    public IReadOnlyList<InventoryCleanupRecommendationViewModel> InventoryRecommendationItems => InventoryRecommendations.Select(item => new InventoryCleanupRecommendationViewModel(item)).ToArray();
    public int InventoryItemCount => InventorySnapshot?.Items.Count ?? 0;
    public int InventoryOlderVersionCount => InventorySnapshot?.Items.Count(item => item.Signals.Contains(InventorySignal.OlderVersion)) ?? 0;
    public string InventorySummary => $"Éléments détectés : {InventoryItemCount}";
    public string InventoryOlderSummary => $"Versions potentiellement anciennes : {InventoryOlderVersionCount}";
    public string InventoryProviderHealthSummary => InventorySnapshot is null
        ? "État des sources : —"
        : $"Sources : {InventorySnapshot.ProviderDiagnostics.Count(item => item.Success)} OK · {InventorySnapshot.ProviderDiagnostics.Count(item => !item.Success)} en erreur";

    public async Task InitializeAsync()
    {
        await RefreshBaselineAsync();
        await LoadProvisioningProfilesAsync();
        await ScanInventoryAsync();
        await RecoverProvisioningOperationAsync();
        await LoadProvisioningHistoryAsync();
    }

    private Task NavigateAsync(DesktopPage page)
    {
        CurrentPage = page;
        return Task.CompletedTask;
    }

    private async Task ScanInventoryAsync()
    {
        await RunBusyAsync("Analyse de l’inventaire…", async () =>
        {
            try
            {
                var response = await _engineClient.ExecuteAsync<InventorySnapshot>("inventory");
                if (!response.Success || response.Data is null)
                    throw new InvalidOperationException(response.Error ?? "Impossible d’analyser l’inventaire.");

                var snapshot = response.Data;
                InventorySnapshot = snapshot;
                InventoryRecommendations = new InventoryCleanupAnalyzer()
                    .Analyze(snapshot.Items, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                OnPropertyChanged(nameof(InventoryItemCount));
                OnPropertyChanged(nameof(InventoryOlderVersionCount));
                OnPropertyChanged(nameof(InventorySummary));
                OnPropertyChanged(nameof(InventoryOlderSummary));
                OnPropertyChanged(nameof(InventoryProviderHealthSummary));
                OnPropertyChanged(nameof(InventoryRecommendationItems));
            }
            catch (Exception ex)
            {
                ProvisioningOutput = $"Inventory scan failed: {ex.Message}";
            }
        });
    }

    private async Task RefreshBaselineAsync()
    {
        await RunBusyAsync("Analyse du système…", RefreshBaselineCoreAsync);
    }

    private async Task RefreshBaselineCoreAsync()
    {
            try
            {
                var response = await _engineClient.ExecuteAsync<BaselineData>("baseline");
                if (!response.Success || response.Data is null)
                {
                    throw new InvalidOperationException(response.Error ?? "Analyse du baseline échouée.");
                }

                var data = response.Data;
                WindowsValue = data.Windows ?? "Windows";
                BuildValue = $"Build {data.Build} • {data.Architecture}";
                CpuValue = data.Cpu ?? "CPU";
                CoreValue = $"{data.CpuCores} threads logiques";
                MemoryValue = $"{data.MemoryGB:0.0} Go";
                UptimeValue = $"Uptime : {data.UptimeHours:0.0} h";

                StorageValue = string.Join(
                    Environment.NewLine,
                    data.Volumes.Select(volume =>
                    {
                        var free = volume.SizeRemaining / 1073741824d;
                        var total = volume.Size / 1073741824d;
                        return $"{volume.DriveLetter}: {free:0.0} Go libres / {total:0.0} Go — {volume.HealthStatus}";
                    }));

                var startup = data.StartupEntries
                    .Select(entry => entry.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name));
                StartupValue = startup.Any()
                    ? string.Join(" • ", startup)
                    : "Aucune entrée détectée.";
            }
            catch (Exception ex)
            {
                WindowsValue = "Analyse indisponible";
                BuildValue = ex.Message;
            }
    }

    private async Task LoadProvisioningProfilesAsync()
    {
        await RunBusyAsync("Chargement des profils…", async () =>
        {
            try
            {
                var response = await _engineClient.ExecuteAsync<ProvisioningProfileOption[]>("provisioning-profiles");
                if (!response.Success || response.Data is null)
                {
                    ProvisioningOutput = response.Error ?? "Impossible de charger les profils de provisionnement.";
                    return;
                }

                ProvisioningProfiles = response.Data;
                if (!ProvisioningProfiles.Any(profile => profile.Id == SelectedProvisioningProfileId))
                {
                    SelectedProvisioningProfileId = ProvisioningProfiles.FirstOrDefault()?.Id ?? string.Empty;
                }

                await LoadProvisioningPlanCoreAsync();
            }
            catch (Exception ex)
            {
                ProvisioningOutput = ex.Message;
            }
        });
    }

    private async Task LoadProvisioningHistoryAsync()
    {
        await RunBusyAsync("Chargement de l’historique…", async () =>
        {
            try
            {
                var response = await _engineClient.GetProvisioningHistoryAsync();
                if (!response.Success || response.Data is null)
                {
                    ProvisioningOutput = response.Error ?? "Impossible de charger l’historique.";
                    return;
                }

                ProvisioningHistory = response.Data.Select(item => new ProvisioningOperationHistoryItemViewModel(item)).ToArray();
                SelectedProvisioningHistoryItem = ProvisioningHistory.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ProvisioningOutput = ex.Message;
            }
        });
    }

    private async Task RecoverProvisioningOperationAsync()
    {
        try
        {
            var response = await _engineClient.GetProvisioningRecoveryAsync();
            if (!response.Success || response.Data is null) return;

            ProvisioningOperation = response.Data;
            if (!string.IsNullOrWhiteSpace(response.Data.ProfileId))
                SelectedProvisioningProfileId = response.Data.ProfileId;

            OnPropertyChanged(nameof(IsProvisioningOperationActive));
            OnPropertyChanged(nameof(IsProvisioningOperationVisible));
            OnPropertyChanged(nameof(ProvisioningOperationStatus));
            OnPropertyChanged(nameof(ProvisioningCurrentStep));
            ProvisioningResumeCommand.RaiseCanExecuteChanged();

            if (response.Data.Status is "starting" or "running")
                await PollProvisioningOperationAsync(response.Data.OperationId);
        }
        catch (Exception ex)
        {
            ProvisioningOutput = $"Récupération du provisionnement : {ex.Message}";
        }
    }

    private async Task ExecuteProvisioningAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedProvisioningProfileId))
        {
            return;
        }

        if (!_dialogService.Confirm(
                "Démarrer le provisionnement de ce profil ? Le moteur installera uniquement les composants prévus par le plan.",
                "Confirmer le provisionnement"))
        {
            return;
        }

        await RunBusyAsync("Démarrage du provisionnement…", async () =>
        {
            try
            {
                var response = await _engineClient.StartProvisioningAsync(SelectedProvisioningProfileId);

                if (!response.Success || response.Data is null)
                {
                    ProvisioningOutput = response.Error ?? "Impossible de démarrer le provisionnement.";
                    return;
                }

                await PollProvisioningOperationAsync(response.Data.OperationId);
            }
            catch (Exception ex)
            {
                ProvisioningOutput = ex.Message;
            }
        });
    }

    private async Task PollProvisioningOperationAsync(string operationId)
    {
        _provisioningPollingCancellation?.Cancel();
        _provisioningPollingCancellation?.Dispose();
        _provisioningPollingCancellation = new CancellationTokenSource();
        var token = _provisioningPollingCancellation.Token;

        while (!token.IsCancellationRequested)
        {
            var response = await _engineClient.GetProvisioningStatusAsync(operationId, token);

            if (!response.Success || response.Data is null)
            {
                ProvisioningOutput = response.Error ?? "Impossible de lire l'état du provisionnement.";
                return;
            }

            ProvisioningOperation = response.Data;
            OnPropertyChanged(nameof(IsProvisioningOperationActive));
            OnPropertyChanged(nameof(IsProvisioningOperationVisible));
            OnPropertyChanged(nameof(ProvisioningOperationStatus));
            OnPropertyChanged(nameof(ProvisioningCurrentStep));

            if (response.Data.Status is "completed" or "failed" or "reboot-required")
            {
                ProvisioningOutput = response.Data.Error
                    ?? (response.Data.Status == "completed"
                        ? "Provisionnement terminé."
                        : ProvisioningOperationStatus);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(750), token);
        }
    }

    private async Task ResumeProvisioningAsync()
    {
        if (ProvisioningOperation is null || !ProvisioningOperation.CanResume) return;

        if (!_dialogService.Confirm(
                "Reprendre cette opération à l'étape sauvegardée ? Les composants déjà conformes seront vérifiés avant toute installation.",
                "Reprendre le provisionnement"))
            return;

        await RunBusyAsync("Reprise du provisionnement…", async () =>
        {
            try
            {
                var response = await _engineClient.ResumeProvisioningAsync(ProvisioningOperation.OperationId);
                if (!response.Success || response.Data is null)
                {
                    ProvisioningOutput = response.Error ?? "Impossible de reprendre le provisionnement.";
                    return;
                }

                await PollProvisioningOperationAsync(response.Data.OperationId);
            }
            catch (Exception ex)
            {
                ProvisioningOutput = ex.Message;
            }
        });
    }

    private async Task LoadProvisioningPlanAsync()
    {
        await RunBusyAsync("Analyse du plan de provisionnement…", LoadProvisioningPlanCoreAsync);
    }

    private async Task LoadProvisioningPlanCoreAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedProvisioningProfileId))
        {
            return;
        }

        try
        {
            var response = await _engineClient.ExecuteAsync<ProvisioningPlanData>(
                "provisioning-plan",
                SelectedProvisioningProfileId);
            if (!response.Success || response.Data is null)
            {
                ProvisioningOutput = response.Error ?? "Impossible de charger le plan de provisionnement.";
                ProvisioningPlanItems = Array.Empty<ProvisioningPlanItemViewModel>();
                return;
            }

            ProvisioningProfileDescription = response.Data.Profile.Description;
            ProvisioningPlanItems = response.Data.Items
                .Select(item => new ProvisioningPlanItemViewModel(item))
                .ToArray();
            ProvisioningOutput = $"{ProvisioningPlanItems.Count} composants analysés pour {response.Data.Profile.Name}.";
        }
        catch (Exception ex)
        {
            ProvisioningOutput = ex.Message;
            ProvisioningPlanItems = Array.Empty<ProvisioningPlanItemViewModel>();
        }
    }

    private async Task LoadOptimizationPlanAsync()
    {
        await RunBusyAsync("Lecture du plan d'optimisation…", async () =>
        {
            try
            {
                var response = await _engineClient.ExecuteAsync<OptimizationPlanItem[]>("optimization-plan-safe");
                if (!response.Success || response.Data is null)
                {
                    OptimizationOutput = response.Error ?? "Impossible de charger le plan d'optimisation.";
                    return;
                }

                OptimizationOutput = string.Join(Environment.NewLine, response.Data.Select(item =>
                {
                    var (marker, label) = item.StatusCode switch
                    {
                        "applied" => ("✓", "Déjà appliqué"),
                        "compliant" => ("≈", "Déjà conforme"),
                        "drifted" => ("⚠", "Modifié depuis l'application"),
                        "pending" => ("•", "À appliquer"),
                        _ => ("?", "État inconnu")
                    };

                    return $"{marker} {item.Name} — {label} — {item.Impact} — {item.Risk}";
                }));
            }
            catch (Exception ex)
            {
                OptimizationOutput = ex.Message;
            }
        });
    }

    private async Task ApplyOptimizationAsync()
    {
        if (!_dialogService.Confirm(
                "Appliquer les optimisations Safe ? Les changements sont limités au catalogue Safe et enregistrés pour permettre un rollback.",
                "Confirmer"))
        {
            return;
        }

        await RunBusyAsync("Application des optimisations Safe…", async () =>
        {
            try
            {
                var response = await _engineClient.ExecuteAsync<object>("optimization-apply-safe");
                OptimizationOutput = response.Success
                    ? string.Join(Environment.NewLine, response.Messages)
                    : response.Error ?? "L'optimisation a échoué.";
            }
            catch (Exception ex)
            {
                OptimizationOutput = ex.Message;
            }

            await RefreshBaselineCoreAsync();
        });
    }

    private async Task RollbackOptimizationAsync()
    {
        if (!_dialogService.Confirm(
                "Restaurer les optimisations réversibles enregistrées ?",
                "Confirmer"))
        {
            return;
        }

        await RunBusyAsync("Restauration des optimisations…", async () =>
        {
            try
            {
                var response = await _engineClient.ExecuteAsync<object>("optimization-rollback");
                OptimizationOutput = response.Success
                    ? string.Join(Environment.NewLine, response.Messages)
                    : response.Error ?? "Le rollback a échoué.";
            }
            catch (Exception ex)
            {
                OptimizationOutput = ex.Message;
            }

            await RefreshBaselineCoreAsync();
        });
    }

    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyMessage = message;
        RaiseCommandCanExecuteChanged();
        try
        {
            await operation();
        }
        finally
        {
            BusyMessage = string.Empty;
            IsBusy = false;
            RaiseCommandCanExecuteChanged();
        }
    }

    private async Task LoadProvisioningDetailAsync()
    {
        if (SelectedProvisioningHistoryItem is null) return;

        await RunBusyAsync("Chargement du détail…", async () =>
        {
            try
            {
                var response = await _engineClient.GetProvisioningDetailAsync(SelectedProvisioningHistoryItem.OperationId);
                if (!response.Success || response.Data is null)
                {
                    ProvisioningOutput = response.Error ?? "Impossible de charger le détail de l’opération.";
                    return;
                }

                ProvisioningOperationDetail = response.Data;
            }
            catch (Exception ex)
            {
                ProvisioningOutput = ex.Message;
            }
        });
    }

    private void RaiseCommandCanExecuteChanged()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        OptimizationPlanCommand.RaiseCanExecuteChanged();
        OptimizationApplyCommand.RaiseCanExecuteChanged();
        OptimizationRollbackCommand.RaiseCanExecuteChanged();
        ProvisioningPlanCommand.RaiseCanExecuteChanged();
        ProvisioningExecuteCommand.RaiseCanExecuteChanged();
        ProvisioningResumeCommand.RaiseCanExecuteChanged();
        ProvisioningHistoryCommand.RaiseCanExecuteChanged();
        ProvisioningDetailCommand.RaiseCanExecuteChanged();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


public sealed class InventoryCleanupRecommendationViewModel
{
    private readonly InventoryCleanupRecommendation _item;

    public InventoryCleanupRecommendationViewModel(InventoryCleanupRecommendation item) => _item = item;

    public string DisplayName => _item.DisplayName;
    public string InstalledVersionLabel => string.IsNullOrWhiteSpace(_item.InstalledVersion)
        ? "Version installée : inconnue"
        : $"Version installée : {_item.InstalledVersion}";
    public string NewerVersionLabel => string.IsNullOrWhiteSpace(_item.NewerVersion)
        ? "Aucune version plus récente identifiée"
        : $"Version plus récente détectée : {_item.NewerVersion}";
    public string PublisherLabel => string.IsNullOrWhiteSpace(_item.Publisher)
        ? "Éditeur : inconnu"
        : $"Éditeur : {_item.Publisher}";
    public string ReasonLabel => _item.ReasonKey;
    public string EvidenceLabel => string.Join(" · ", _item.Evidence);
    public string ApprovalLabel => _item.RequiresExplicitApproval
        ? "Suppression uniquement après validation explicite."
        : "Action sans approbation explicite.";
}


public sealed record ThemeOption(ThemeMode Mode, string Label);
