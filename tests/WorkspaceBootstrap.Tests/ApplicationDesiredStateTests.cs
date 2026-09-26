using WorkspaceControl.Domain;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class ApplicationDesiredStateTests
{
    [TestMethod]
    public void Desired_state_codes_are_distinct_from_observed_state_codes()
    {
        Assert.AreEqual("undefined", ApplicationDesiredStateCodes.Undefined);
        Assert.AreEqual("present", ApplicationDesiredStateCodes.Present);
        Assert.AreEqual("absent", ApplicationDesiredStateCodes.Absent);

        Assert.AreEqual("installed", ApplicationObservedStateCodes.Installed);
        Assert.AreEqual("not-installed", ApplicationObservedStateCodes.NotInstalled);
        Assert.AreEqual("unknown", ApplicationObservedStateCodes.Unknown);
    }

    [TestMethod]
    public void Workspace_application_defaults_to_present()
    {
        var application = new WorkspaceApplication("firefox");

        Assert.AreEqual(ApplicationDesiredStateCodes.Present, application.State);
    }

    [TestMethod]
    public void Workspace_application_can_explicitly_request_absence()
    {
        var application = new WorkspaceApplication(
            "xbox",
            State: ApplicationDesiredStateCodes.Absent);

        Assert.AreEqual(ApplicationDesiredStateCodes.Absent, application.State);
    }

    [TestMethod]
    public void Undefined_is_not_an_executable_application_request()
    {
        var application = new WorkspaceApplication(
            "spotify",
            State: ApplicationDesiredStateCodes.Undefined);

        Assert.AreEqual(ApplicationDesiredStateCodes.Undefined, application.State);
        Assert.AreNotEqual(ApplicationDesiredStateCodes.Present, application.State);
        Assert.AreNotEqual(ApplicationDesiredStateCodes.Absent, application.State);
    }
}
