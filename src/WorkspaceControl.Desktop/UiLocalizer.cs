using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace WorkspaceControl.Desktop;

public interface IUiLocalizer
{
    string Get(string key);
    string Format(string key, params object[] arguments);
}

public sealed class WinUiLocalizer : IUiLocalizer
{
    private readonly ResourceLoader _resourceLoader = new();

    public string Get(string key) =>
        _resourceLoader.GetString(key);

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
