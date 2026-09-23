using Microsoft.Win32;

namespace WorkspaceControl.Infrastructure;

public sealed class WindowsRegistryUninstallInventoryProvider : IInventoryProvider
{
    private static readonly RegistryView[] Views =
    [
        RegistryView.Registry64,
        RegistryView.Registry32
    ];

    private const string UninstallPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public string Id => "windows.registry.uninstall";

    public Task<InventoryProviderResult> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        var observations = new List<InventoryObservation>();

        ScanHive(RegistryHive.LocalMachine, InventoryScope.System, observations, cancellationToken);
        ScanHive(RegistryHive.CurrentUser, InventoryScope.User, observations, cancellationToken);

        return Task.FromResult(new InventoryProviderResult(
            observations,
            new InventoryProviderDiagnostic(Id, true, "Registry uninstall inventory scan completed.")));
    }

    private void ScanHive(
        RegistryHive hive,
        InventoryScope scope,
        ICollection<InventoryObservation> observations,
        CancellationToken cancellationToken)
    {
        foreach (var view in Views)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallPath);
            if (uninstall is null)
            {
                continue;
            }

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var entry = uninstall.OpenSubKey(subKeyName);
                if (entry is null) continue;
                var displayName = entry.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                var version = entry.GetValue("DisplayVersion") as string;
                var installLocation = entry.GetValue("InstallLocation") as string;
                var publisher = entry.GetValue("Publisher") as string;
                var uninstallCommand = entry.GetValue("UninstallString") as string;
                var isWindowsInstaller = entry.GetValue("WindowsInstaller") is int windowsInstaller && windowsInstaller == 1;
                var providerId = $"{hive}:{view}:{subKeyName}";
                var id = CreateInstanceIdentity(publisher, displayName, version, installLocation, subKeyName);

                observations.Add(new InventoryObservation(
                    id,
                    displayName.Trim(),
                    string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
                    InferFamilyId(displayName),
                    Id,
                    providerId,
                    "registry",
                    scope,
                    string.IsNullOrWhiteSpace(installLocation) ? null : installLocation.Trim(),
                    string.IsNullOrWhiteSpace(uninstallCommand) ? null : uninstallCommand.Trim(),
                    string.IsNullOrWhiteSpace(publisher) ? null : publisher.Trim(),
                    isWindowsInstaller
                        ? InventoryOwnership.ProductManaged
                        : InventoryOwnership.Unknown,
                    Array.Empty<string>(),
                    Array.Empty<InventoryEvidence>(),
                    DateTimeOffset.UtcNow));
            }
        }
    }

    private static string CreateInstanceIdentity(
        string? publisher,
        string displayName,
        string? version,
        string? installLocation,
        string subKeyName)
    {
        // FamilyId groups versions; Id identifies an installed instance.
        // This intentionally keeps side-by-side Java/.NET/Node versions visible.
        var identity = $"{publisher}|{displayName}|{version}|{installLocation}|{subKeyName}";
        return $"windows.registry.instance.{Normalize(identity)}";
    }

    private static string? InferFamilyId(string displayName)
    {
        var value = displayName.ToLowerInvariant();

        if (value.Contains("java") || value.Contains("jdk"))
        {
            return "java";
        }

        if (value.Contains(".net"))
        {
            return "dotnet";
        }

        if (value.Contains("node.js") || value == "nodejs")
        {
            return "nodejs";
        }

        if (value.Contains("python"))
        {
            return "python";
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }
}
