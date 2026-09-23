namespace WorkspaceControl.Domain;

public sealed record DesiredStateManifest(
    IReadOnlyList<ProfileApplication> Applications,
    IReadOnlyList<WindowsSettingDesiredState> WindowsSettings,
    IReadOnlyList<PolicyDesiredState> Policies,
    IReadOnlyList<RegistrySettingDesiredState> RegistrySettings,
    IReadOnlyList<OptimizationDesiredState> Optimizations,
    IReadOnlyList<MachineCondition> Conditions);

public sealed record ProfileApplication(
    string ComponentId,
    string? VersionPolicy = null,
    string? MinimumVersion = null);

public sealed record WindowsSettingDesiredState(
    string Id,
    string Value,
    string Scope = "machine");

public sealed record PolicyDesiredState(
    string Id,
    string Value,
    string Scope = "machine");

public sealed record RegistrySettingDesiredState(
    string Hive,
    string Key,
    string ValueName,
    string Value,
    string ValueType,
    string Scope = "machine");

public sealed record OptimizationDesiredState(
    string Id,
    bool Enabled);

public sealed record MachineCondition(
    string Fact,
    string Operator,
    string Value);