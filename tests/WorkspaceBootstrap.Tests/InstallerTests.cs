using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspaceBootstrap;

namespace WorkspaceBootstrap.Tests;

[TestClass]
public sealed class InstallerTests
{
    [TestMethod]
    public async Task Cache_only_install_fails_when_no_verified_artifact_exists()
    {
        var component = new ComponentManifest(
            Id: "test-cache-only-missing-9d8e4b",
            Name: "Test cache-only component",
            Profiles: [],
            PackageId: "test.cache.only.missing",
            Source: null,
            InstallerType: "exe",
            Locale: "en-US",
            Architecture: "x64",
            OfficialSource: null,
            InstallArguments: [],
            FallbackPackageManager: null);

        var engine = new InstallerEngine(new WorkspacePaths());

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => engine.InstallAsync(component, cacheOnly: true, CancellationToken.None));

        StringAssert.Contains(exception.Message, "Aucun installateur vérifié");
    }
}
