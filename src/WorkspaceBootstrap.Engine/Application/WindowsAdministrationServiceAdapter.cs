using WorkspaceControl.Application.Contracts;

namespace WorkspaceBootstrap.Application;

internal sealed class WindowsAdministrationServiceAdapter : IWindowsAdministrationService
{
    private readonly WindowsSystemService _service = new();

    public BaselineSnapshot GetBaseline() => _service.GetBaseline();
}
