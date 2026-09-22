using System.IO;
using System.Text.Json.Serialization;

namespace WorkspaceBootstrap.Desktop;

public sealed record ConfigurationProfile(
    int SchemaVersion,
    string ProfileId,
    IReadOnlyList<ConfigurationComponent> Components,
    ConfigurationPolicies Policies,
    ConfigurationPreferences Preferences,
    ConfigurationCompatibility? Compatibility = null);

public sealed record ConfigurationComponent(
    string Id,
    JobVersionPolicy? Version = null);

public sealed record ConfigurationPolicies(ConfigurationNetworkPolicy Network);

public sealed record ConfigurationNetworkPolicy(
    string Mode,
    int MaxConcurrentDownloads,
    long? MaxDownloadBytesPerSecond = null);

public sealed record ConfigurationPreferences(string Language);

public sealed record ConfigurationCompatibility(
    string? ProductVersion = null,
    string? MinimumProductVersion = null);

public static class ConfigurationProfileValidator
{
    public static void Validate(ConfigurationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported configuration schema version: {profile.SchemaVersion}.");

        if (string.IsNullOrWhiteSpace(profile.ProfileId))
            throw new InvalidDataException("Configuration profile ID is required.");

        if (profile.Components.Any(component => component is null || string.IsNullOrWhiteSpace(component.Id)))
            throw new InvalidDataException("Configuration component IDs must not be empty.");

        if (profile.Components.Count != profile.Components.Select(component => component.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidDataException("Configuration component IDs must be unique.");

        foreach (var component in profile.Components)
        {
            ValidateVersionPolicy(component.Version, component.Id);
        }

        if (!string.Equals(profile.Policies.Network.Mode, "unlimited", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(profile.Policies.Network.Mode, "limited", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(profile.Policies.Network.Mode, "paused", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported network policy mode: {profile.Policies.Network.Mode}.");

        if (profile.Policies.Network.MaxConcurrentDownloads < 1)
            throw new InvalidDataException("Maximum concurrent downloads must be at least one.");

        if (profile.Policies.Network.MaxDownloadBytesPerSecond is <= 0)
            throw new InvalidDataException("Maximum download rate must be positive when specified.");

        if (string.IsNullOrWhiteSpace(profile.Preferences.Language))
            throw new InvalidDataException("Configuration language preference is required.");
    }

    private static void ValidateVersionPolicy(JobVersionPolicy? policy, string componentId)
    {
        if (policy is null || policy.Mode == VersionSelectionMode.Latest)
            return;

        if (policy.Mode == VersionSelectionMode.Channel)
        {
            if (string.IsNullOrWhiteSpace(policy.Channel))
                throw new InvalidDataException($"Version channel is required for component: {componentId}.");
            return;
        }

        if (string.IsNullOrWhiteSpace(policy.Value))
            throw new InvalidDataException($"Version value is required for component: {componentId}.");
    }
}
