using System.Diagnostics;
using System.Windows;

namespace WorkspaceBootstrap.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Trace.TraceError($"Unhandled WPF exception: {args.Exception}");
            MessageBox.Show(
                "Une erreur inattendue s'est produite. Consultez les journaux pour plus de détails.",
                "Workspace Bootstrap",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
