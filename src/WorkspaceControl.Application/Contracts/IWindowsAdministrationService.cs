using WorkspaceBootstrap;

namespace WorkspaceControl.Application.Contracts;

public interface IWindowsAdministrationService
{
    BaselineSnapshot GetBaseline();
}
