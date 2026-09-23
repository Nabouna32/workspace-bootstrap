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

public static class DesiredStateDomainCodes
{
    public const string Application = "application";
    public const string WindowsSetting = "windows-setting";
    public const string Policy = "policy";
    public const string RegistrySetting = "registry-setting";
    public const string Optimization = "optimization";
    public const string Condition = "condition";
}

public static class DesiredStateChangeCodes
{
    public const string Compliant = "compliant";
    public const string Install = "install";
    public const string Update = "update";
    public const string Blocked = "blocked";
    public const string Unknown = "unknown";
}

public sealed record DesiredStateDiffItem(
    string Domain,
    string TargetId,
    string TargetName,
    string StateCode,
    string ActionCode,
    string? ObservedValue,
    string? AvailableValue,
    string? DesiredValue,
    string Message);

public sealed record DesiredStateDiff(
    string ProfileId,
    string ProfileName,
    string InventoryScanId,
    IReadOnlyList<InventoryProviderDiagnostic> InventoryDiagnostics,
    IReadOnlyList<DesiredStateDiffItem> Items,
    DateTimeOffset CreatedAtUtc);
