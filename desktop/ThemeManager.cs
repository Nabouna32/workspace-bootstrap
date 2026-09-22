using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace WorkspaceBootstrap.Desktop;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed class ThemeManager
{
    private readonly string _settingsPath;

    public ThemeManager()
    {
        var stateRoot = @"C:\Dev\WorkspaceBootstrap";
        Directory.CreateDirectory(stateRoot);
        _settingsPath = Path.Combine(stateRoot, "desktop-settings.json");
    }

    public ThemeMode LoadMode()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return ThemeMode.System;

            var value = File.ReadAllText(_settingsPath).Trim();
            return Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode)
                ? mode
                : ThemeMode.System;
        }
        catch
        {
            return ThemeMode.System;
        }
    }

    public void Apply(ThemeMode mode)
    {
        var palette = GetPalette(ResolveSystemTheme(mode));
        var resources = Application.Current.Resources;

        foreach (var pair in palette)
            resources[pair.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));

        try
        {
            File.WriteAllText(_settingsPath, mode.ToString());
        }
        catch
        {
            // The selected theme remains active for the current session if persistence is unavailable.
        }
    }

    private static ThemeMode ResolveSystemTheme(ThemeMode mode)
    {
        if (mode != ThemeMode.System)
            return mode;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1
                ? ThemeMode.Light
                : ThemeMode.Dark;
        }
        catch
        {
            return ThemeMode.Dark;
        }
    }

    private static IReadOnlyDictionary<string, string> GetPalette(ThemeMode mode) =>
        mode == ThemeMode.Light
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WindowBackground"] = "#F6F7F9",
                ["SidebarBackground"] = "#FFFFFF",
                ["SidebarSelected"] = "#E8F0FE",
                ["CardBackground"] = "#FFFFFF",
                ["CardBackgroundElevated"] = "#F9FAFC",
                ["CardBorder"] = "#D9DEE7",
                ["PrimaryText"] = "#17202A",
                ["SecondaryText"] = "#5E6A78",
                ["Accent"] = "#1769E0",
                ["AccentSoft"] = "#E4EEFF",
                ["Success"] = "#16875B",
                ["SuccessSoft"] = "#DDF6EA",
                ["Warning"] = "#A56A00",
                ["WarningSoft"] = "#FFF0CF",
                ["Danger"] = "#C52B3A",
                ["DangerSoft"] = "#FFE2E5",
                ["Info"] = "#1769E0",
                ["InfoSoft"] = "#E4EEFF"
            }
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WindowBackground"] = "#0B0D10",
                ["SidebarBackground"] = "#11151A",
                ["SidebarSelected"] = "#1D2633",
                ["CardBackground"] = "#14191F",
                ["CardBackgroundElevated"] = "#181E26",
                ["CardBorder"] = "#28313C",
                ["PrimaryText"] = "#F5F7FA",
                ["SecondaryText"] = "#9AA6B2",
                ["Accent"] = "#78A9FF",
                ["AccentSoft"] = "#20334F",
                ["Success"] = "#63D6A0",
                ["SuccessSoft"] = "#173A2D",
                ["Warning"] = "#F3C969",
                ["WarningSoft"] = "#423618",
                ["Danger"] = "#FF7D86",
                ["DangerSoft"] = "#452028",
                ["Info"] = "#78A9FF",
                ["InfoSoft"] = "#20334F"
            };
}
