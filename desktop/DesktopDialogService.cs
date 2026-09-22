using System.Windows;

namespace BounaDevEnvironment;

public sealed class DesktopDialogService
{
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
