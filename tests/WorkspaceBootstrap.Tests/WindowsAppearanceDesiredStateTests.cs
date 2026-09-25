using WorkspaceControl.Domain;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class WindowsAppearanceDesiredStateTests
{
    [TestMethod]
    public void ReadReturnsLightWhenBothWindowsAppearanceValuesAreLight()
    {
        var settings = CreateSettings("1");

        Assert.AreEqual(WindowsAppearanceValues.Light, WindowsAppearanceSettings.Read(settings));
    }

    [TestMethod]
    public void ReadReturnsDarkWhenBothWindowsAppearanceValuesAreDark()
    {
        var settings = CreateSettings("0");

        Assert.AreEqual(WindowsAppearanceValues.Dark, WindowsAppearanceSettings.Read(settings));
    }

    [TestMethod]
    public void ReadReturnsUndefinedWhenAppearanceIsIncomplete()
    {
        var settings = new[]
        {
            new RegistrySettingDesiredState(
                "HKCU",
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                "1",
                "dword",
                "user")
        };

        Assert.AreEqual(WindowsAppearanceValues.Undefined, WindowsAppearanceSettings.Read(settings));
    }

    [TestMethod]
    public void ApplyReplacesKnownAppearanceValuesWithoutTouchingOtherSettings()
    {
        var unrelated = new RegistrySettingDesiredState(
            "HKCU",
            @"Software\Example",
            "Value",
            "keep",
            "string",
            "user");

        var settings = CreateSettings("1").Append(unrelated).ToArray();
        var updated = WindowsAppearanceSettings.Apply(settings, WindowsAppearanceValues.Dark);

        Assert.AreEqual(3, updated.Count);
        Assert.IsTrue(updated.Contains(unrelated));
        Assert.IsTrue(updated.Any(setting => setting.ValueName == "AppsUseLightTheme" && setting.Value == "0"));
        Assert.IsTrue(updated.Any(setting => setting.ValueName == "SystemUsesLightTheme" && setting.Value == "0"));
    }

    [TestMethod]
    public void ApplyUndefinedRemovesAppearanceIntent()
    {
        var settings = CreateSettings("1");

        var updated = WindowsAppearanceSettings.Apply(settings, WindowsAppearanceValues.Undefined);

        Assert.AreEqual(0, updated.Count);
    }

    private static RegistrySettingDesiredState[] CreateSettings(string value) =>
    [
        new(
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            value,
            "dword",
            "user"),
        new(
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme",
            value,
            "dword",
            "user")
    ];
}
