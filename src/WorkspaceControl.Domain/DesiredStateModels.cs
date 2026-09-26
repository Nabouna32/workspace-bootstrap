namespace WorkspaceControl.Domain;

public sealed record DesiredStateManifest(
    IReadOnlyList<WorkspaceApplication> Applications,
    IReadOnlyList<WindowsSettingDesiredState> WindowsSettings,
    IReadOnlyList<PolicyDesiredState> Policies,
    IReadOnlyList<RegistrySettingDesiredState> RegistrySettings,
    IReadOnlyList<OptimizationDesiredState> Optimizations,
    IReadOnlyList<MachineCondition> Conditions);

public static class WorkspaceApplicationIntentCodes
{
    public const string Undefined = "undefined";
    public const string Present = "present";
    public const string Absent = "absent";
}

public sealed record WorkspaceApplication(
    string ComponentId,
    string? VersionPolicy = null,
    string? MinimumVersion = null,
    string State = WorkspaceApplicationIntentCodes.Present);

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

public static class DesiredStateStateCodes
{
    public const string Compliant = "COMPLIANT";
    public const string Drifted = "DRIFTED";
}

public static class DesiredStateDomainCodes
{
    public const string Application = "application";
    public const string WindowsSetting = "windows-setting";
    public const string Policy = "policy";
    public const string RegistrySetting = "registry-setting";
    public const string Optimization = "optimization";
    public const string Condition = "condition";
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
    string WorkspaceId,
    string WorkspaceName,
    string InventoryScanId,
    IReadOnlyList<InventoryProviderDiagnostic> InventoryDiagnostics,
    IReadOnlyList<DesiredStateDiffItem> Items,
    DateTimeOffset CreatedAtUtc);


public sealed record ConditionEvaluation(
    MachineCondition Condition,
    bool IsKnown,
    bool IsSatisfied,
    string Message);

public static class MachineConditionEvaluator
{
    public static IReadOnlyList<ConditionEvaluation> Evaluate(
        IReadOnlyList<MachineCondition> conditions,
        BaselineSnapshot baseline)
    {
        return conditions
            .Select(condition => Evaluate(condition, baseline))
            .ToArray();
    }

    public static ConditionEvaluation Evaluate(
        MachineCondition condition,
        BaselineSnapshot baseline)
    {
        if (!TryGetFact(baseline, condition.Fact, out var observed))
        {
            return new ConditionEvaluation(
                condition,
                false,
                false,
                $"Machine fact '{condition.Fact}' is not available.");
        }

        var satisfied = Compare(observed, condition.Operator, condition.Value);

        return new ConditionEvaluation(
            condition,
            true,
            satisfied,
            satisfied
                ? $"Condition '{condition.Fact} {condition.Operator} {condition.Value}' is satisfied by '{observed}'."
                : $"Condition '{condition.Fact} {condition.Operator} {condition.Value}' is not satisfied by '{observed}'.");
    }

    private static bool TryGetFact(
        BaselineSnapshot baseline,
        string fact,
        out string value)
    {
        switch (fact.Trim().ToLowerInvariant())
        {
            case "windows":
            case "windows.version":
                value = baseline.Windows;
                return true;
            case "build":
            case "windows.build":
                value = baseline.Build;
                return true;
            case "architecture":
            case "machine.architecture":
                value = baseline.Architecture;
                return true;
            case "cpu":
            case "machine.cpu":
                value = baseline.Cpu;
                return true;
            case "cpucount":
            case "cpucores":
            case "machine.cpucores":
                value = baseline.CpuCores.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            case "memorygb":
            case "machine.memorygb":
                value = baseline.MemoryGB.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            case "uptimehours":
            case "machine.uptimehours":
                value = baseline.UptimeHours.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private static bool Compare(string observed, string @operator, string expected)
    {
        switch (@operator.Trim().ToLowerInvariant())
        {
            case "equals":
                return string.Equals(observed, expected, StringComparison.OrdinalIgnoreCase);
            case "not-equals":
                return !string.Equals(observed, expected, StringComparison.OrdinalIgnoreCase);
            case "contains":
                return observed.Contains(expected, StringComparison.OrdinalIgnoreCase);
            case "greater-than":
            case "less-than":
            case "greater-or-equal":
            case "less-or-equal":
                var comparison = CompareOrdered(observed, expected);
                return @operator.Trim().ToLowerInvariant() switch
                {
                    "greater-than" => comparison > 0,
                    "less-than" => comparison < 0,
                    "greater-or-equal" => comparison >= 0,
                    "less-or-equal" => comparison <= 0,
                    _ => false
                };
            default:
                return false;
        }
    }

    private static int CompareOrdered(string observed, string expected)
    {
        if (double.TryParse(observed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var observedNumber)
            && double.TryParse(expected, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var expectedNumber))
        {
            return observedNumber.CompareTo(expectedNumber);
        }

        if (Version.TryParse(observed, out var observedVersion)
            && Version.TryParse(expected, out var expectedVersion))
        {
            return observedVersion.CompareTo(expectedVersion);
        }

        return string.Compare(observed, expected, StringComparison.OrdinalIgnoreCase);
    }
}
