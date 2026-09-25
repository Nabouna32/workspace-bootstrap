namespace WorkspaceControl.Domain;

public static class WindowsAppearanceValues
{
    public const string Undefined = "undefined";
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";
}

public static class WindowsAppearanceSettings
{
    private const string Hive = "HKCU";
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsValue = "AppsUseLightTheme";
    private const string SystemValue = "SystemUsesLightTheme";

    public static string Read(IReadOnlyList<RegistrySettingDesiredState> settings)
    {
        var relevant = settings
            .Where(setting =>
                string.Equals(setting.Hive, Hive, StringComparison.OrdinalIgnoreCase)
                && string.Equals(setting.Key, Key, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var apps = relevant.FirstOrDefault(setting =>
            string.Equals(setting.ValueName, AppsValue, StringComparison.OrdinalIgnoreCase))?.Value;
        var system = relevant.FirstOrDefault(setting =>
            string.Equals(setting.ValueName, SystemValue, StringComparison.OrdinalIgnoreCase))?.Value;

        if (apps is null && system is null)
            return WindowsAppearanceValues.System;

        if (apps == "0" && system == "0")
            return WindowsAppearanceValues.Dark;

        if (apps == "1" && system == "1")
            return WindowsAppearanceValues.Light;

        return WindowsAppearanceValues.Undefined;
    }

    public static IReadOnlyList<RegistrySettingDesiredState> Apply(
        IReadOnlyList<RegistrySettingDesiredState> settings,
        string appearance)
    {
        var remaining = settings
            .Where(setting =>
                !(
                    string.Equals(setting.Hive, Hive, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(setting.Key, Key, StringComparison.OrdinalIgnoreCase)
                    && (
                        string.Equals(setting.ValueName, AppsValue, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(setting.ValueName, SystemValue, StringComparison.OrdinalIgnoreCase)
                    )
                ))
            .ToList();

        if (appearance is not WindowsAppearanceValues.Light and not WindowsAppearanceValues.Dark)
            return remaining;

        var value = appearance == WindowsAppearanceValues.Light ? "1" : "0";
        remaining.Add(new RegistrySettingDesiredState(Hive, Key, AppsValue, value, "dword", "user"));
        remaining.Add(new RegistrySettingDesiredState(Hive, Key, SystemValue, value, "dword", "user"));
        return remaining;
    }
}
