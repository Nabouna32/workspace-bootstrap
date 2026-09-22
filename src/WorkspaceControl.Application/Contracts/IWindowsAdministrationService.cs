using WorkspaceControl.Domain;


namespace WorkspaceControl.Application.Contracts;

public interface IWindowsAdministrationService
{
    BaselineSnapshot GetBaseline();
}
