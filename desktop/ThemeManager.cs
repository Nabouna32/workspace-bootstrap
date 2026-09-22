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
        var palette = ResolvePalette(mode);
        var resources = Application.Current.Resources;

        foreach (var pair in palette)
            resources[pair.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));

        resources["ThemeModeLabel"] = mode switch
        {
            ThemeMode.Dark => "Sombre",
            ThemeMode.Light => "Clair",
            _ => "Système"
        };

        try
        {
            File.WriteAllText(_settingsPath, mode.ToString());
        }
        catch
        {
            // Theme selection remains valid for the current session if persistence is unavailable.
        }
    }

    private static ThemeMode ResolvePalette(ThemeMode mode)
    {
        if (mode != ThemeMode.System)
            return mode;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var useLight = key?.GetValue("AppsUseLightTheme");
            return useLight is int value && value == 1
                ? ThemeMode.Light
                : ThemeMode.Dark;
        }
        catch
        {
            return ThemeMode.Dark;
        }
    }

    private static IReadOnlyDictionary<string, string> Palette(ThemeMode mode) =>
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

    private static IReadOnlyDictionary<string, string> ResolvePaletteInternal(ThemeMode mode) =>
        Palette(mode == ThemeMode.System ? ResolvePalette(ThemeMode.System) : mode);

    private static IReadOnlyDictionary<string, string> ResolvePalette(ThemeMode mode, bool _ = false) =>
        ResolvePaletteInternal(mode);
}
