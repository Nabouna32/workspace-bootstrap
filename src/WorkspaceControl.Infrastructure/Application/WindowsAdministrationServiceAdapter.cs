using WorkspaceControl.Application.Contracts;

namespace WorkspaceControl.Infrastructure.Application;

internal sealed class WindowsAdministrationServiceAdapter : IWindowsAdministrationService
{
    private readonly WindowsSystemService _service = new();

    public BaselineSnapshot GetBaseline() => _service.GetBaseline();
}
