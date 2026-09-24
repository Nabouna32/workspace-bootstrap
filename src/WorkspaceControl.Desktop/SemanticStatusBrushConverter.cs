using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WorkspaceControl.Desktop;

public sealed class SemanticStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var code = value?.ToString()?.Trim().ToUpperInvariant() ?? string.Empty;
        var kind = parameter?.ToString()?.Trim().ToLowerInvariant() ?? "state";

        var key = kind == "action"
            ? code switch
            {
                "REMOVE" => "SystemFillColorCriticalBrush",
                "INSTALL" or "UPDATE" or "SET" => "AccentFillColorDefaultBrush",
                "BLOCKED" => "SystemFillColorCautionBrush",
                "NONE" => "SystemFillColorSuccessBrush",
                "HEALTHY" => "SystemFillColorSuccessBrush",
                "WARNING" => "SystemFillColorCautionBrush",
                "ERROR" => "SystemFillColorCriticalBrush",
                _ => "TextFillColorSecondaryBrush"
            }
            : code switch
            {
                "INSTALLED" or "HEALTHY" => "SystemFillColorSuccessBrush",
                "ERROR" => "SystemFillColorCriticalBrush",
                "UNKNOWN" or "BLOCKED" or "WARNING" => "SystemFillColorCautionBrush",
                "OUTDATED" or "MISSING" or "ABSENT" => "SystemFillColorAttentionBrush",
                _ => "TextFillColorSecondaryBrush"
            };

        return Microsoft.UI.Xaml.Application.Current.Resources[key] as Brush
            ?? Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush
            ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 100));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
