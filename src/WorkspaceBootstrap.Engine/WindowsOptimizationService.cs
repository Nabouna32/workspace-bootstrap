using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorkspaceBootstrap;

public sealed record OptimizationPlanItem(
    string Id,
    string Name,
    string StatusCode,
    string Impact,
    string Risk,
    bool Reversible,
    string Description);

public sealed class WindowsOptimizationService
{
    private readonly string _statePath = Path.Combine(new WorkspacePaths().StateRoot, "optimization.json");

    private static readonly IReadOnlyList<Definition> Catalog =
    [
        new("privacy-tailored-experiences", "Désactiver les expériences personnalisées", "Privacy", "Faible", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1),
        new("privacy-advertising-id", "Désactiver l'identifiant publicitaire", "Privacy", "Faible", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
        new("performance-game-mode", "Activer le Mode Jeu", "Gaming", "Faible", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
    ];

    public IReadOnlyList<OptimizationPlanItem> GetSafePlan() =>
        Catalog.Select(item =>
        {
            var current = Read(item);
            return new OptimizationPlanItem(
                item.Id,
                item.Name,
                current == item.Value ? "compliant" : "pending",
                item.Impact,
                item.Risk,
                true,
                "Modification réversible enregistrée par le moteur.");
        }).ToArray();

    public IReadOnlyList<string> ApplySafe()
    {
        EnsureAdmin();
        var state = LoadState();
        var messages = new List<string>();

        foreach (var item in Catalog)
        {
            var current = Read(item);
            if (current == item.Value)
            {
                messages.Add($"≈ {item.Name} — déjà conforme.");
                continue;
            }

            var previous = current;
            using var key = Registry.CurrentUser.CreateSubKey(item.Path, writable: true)
                ?? throw new InvalidOperationException($"Impossible d'ouvrir HKCU\\{item.Path}.");
            key.SetValue(item.RegistryValueName, item.Value, RegistryValueKind.DWord);

            state.Applied[item.Id] = previous;
            messages.Add($"✓ {item.Name}");
        }

        SaveState(state);
        return messages;
    }

    public IReadOnlyList<string> Rollback()
    {
        EnsureAdmin();
        var state = LoadState();
        var messages = new List<string>();

        foreach (var item in Catalog.Reverse())
        {
            if (!state.Applied.TryGetValue(item.Id, out var previous))
                continue;

            var current = Read(item);
            if (current != item.Value)
            {
                messages.Add($"⚠ {item.Name} — rollback ignoré : la valeur a changé depuis l'application.");
                continue;
            }

            using var key = Registry.CurrentUser.OpenSubKey(item.Path, writable: true);
            if (previous is null)
                key?.DeleteValue(item.RegistryValueName, false);
            else
                (key ?? Registry.CurrentUser.CreateSubKey(item.Path, true)!).SetValue(item.RegistryValueName, previous.Value, RegistryValueKind.DWord);

            state.Applied.Remove(item.Id);
            messages.Add($"↩ {item.Name}");
        }

        SaveState(state);
        return messages;
    }

    private static int? Read(Definition item)
    {
        using var key = Registry.CurrentUser.OpenSubKey(item.Path);
        var value = key?.GetValue(item.RegistryValueName);
        return value is null ? null : Convert.ToInt32(value);
    }

    private State LoadState()
    {
        if (!File.Exists(_statePath))
            return new State();

        return JsonSerializer.Deserialize<State>(
            File.ReadAllText(_statePath), JsonDefaults.Options) ?? new State();
    }

    private void SaveState(State state) =>
        File.WriteAllText(_statePath, JsonSerializer.Serialize(state, JsonDefaults.Options));

    private static void EnsureAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Cette opération nécessite des privilèges administrateur.");
    }

    private sealed record Definition(
        string Id,
        string Name,
        string Impact,
        string Risk,
        string Path,
        string RegistryValueName,
        int Value);

    private sealed class State
    {
        public Dictionary<string, int?> Applied { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
