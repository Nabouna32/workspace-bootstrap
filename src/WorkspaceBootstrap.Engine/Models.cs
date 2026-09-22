using System.Text.Json.Serialization;

namespace WorkspaceBootstrap;

public sealed record ComponentManifest(
    string Id,
    string Name,
    string[]? Profiles,
    string? PackageId,
    string? Source,
    string? InstallerType,
    string? Locale,
    string? Architecture,
    OfficialSource? OfficialSource,
    string[]? InstallArguments,
    string? FallbackPackageManager);

public sealed record OfficialSource(
    string Type,
    string? Repository = null,
    string? AssetRegex = null,
    string? VersionRegex = null);

public sealed record ProfileManifest(
    string Id,
    string Name,
    string Description,
    string[] Components);

public sealed record InstallerArtifact(
    string ComponentId,
    string Version,
    string FilePath,
    string Source,
    string Url,
    string Sha256);

public sealed record ProvisioningStep(
    string ComponentId,
    string Name,
    string Status,
    string? Error = null);

public sealed class ProvisioningOperation
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public string ProfileId { get; init; } = "";
    public string Status { get; set; } = "starting";
    public int Completed { get; set; }
    public int Total { get; set; }
    public string? CurrentComponentName { get; set; }
    public string? Error { get; set; }
    public bool CanResume { get; set; } = true;
    public List<ProvisioningStep> Steps { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class JsonDefaults
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
