using Microsoft.UI.Xaml.Data;
using WorkspaceControl.Domain;

namespace WorkspaceControl.Desktop;

public sealed class WorkspaceCodeDisplayConverter : IValueConverter
{
    private readonly IUiLocalizer _localizer = new WinUiLocalizer();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var code = value?.ToString()?.Trim() ?? string.Empty;
        var kind = parameter?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

        var key = kind switch
        {
            "intent" => code.ToLowerInvariant() switch
            {
                "present" => "WorkspacePage_ApplicationIntentPresent.Content",
                "absent" => "WorkspacePage_ApplicationIntentAbsent.Content",
                _ => "WorkspacePage_ApplicationIntentUndefined.Content"
            },
            "state" => code.ToUpperInvariant() switch
            {
                ApplicationStateCodes.Installed => "WorkspacePage_ApplicationStateInstalled",
                ApplicationStateCodes.Outdated => "WorkspacePage_ApplicationStateOutdated",
                ApplicationStateCodes.Missing => "WorkspacePage_ApplicationStateMissing",
                ApplicationStateCodes.Absent => "WorkspacePage_ApplicationStateAbsent",
                _ => "WorkspacePage_ApplicationStateUnknown"
            },
            "action" => code.ToLowerInvariant() switch
            {
                DesiredStateActionCodes.Install => "WorkspacePage_ActionInstall",
                DesiredStateActionCodes.Update => "WorkspacePage_ActionUpdate",
                DesiredStateActionCodes.Remove => "WorkspacePage_ActionRemove",
                DesiredStateActionCodes.Blocked => "WorkspacePage_ActionBlocked",
                _ => "WorkspacePage_ActionNone"
            },
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(key))
            return code;

        var localized = _localizer.Get(key);
        return string.IsNullOrWhiteSpace(localized) ? code : localized;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
