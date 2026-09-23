# CI and local builds

## Source of truth
GitHub is the source of truth. The exact commit being merged must be validated by GitHub Actions.

## Runner policy
All workflows use **GitHub-hosted Windows runners**. **Self-hosted runners are not part of Workspace Control and must not be reintroduced.**

## PR validation
Fast validation covers restore, build, unit tests, architecture/static analysis, formatting and contract/schema checks.

The repository validates the native WinUI desktop and the shared .NET application/infrastructure stack. WPF is retired.

## Main / scheduled validation
Heavier validation may run on main, scheduled or manual workflows: full Release builds, published artifacts, CLI smoke tests, Windows integration, provisioning simulations, artifact reuse scenarios, install/update/remove integration, UI/accessibility tests, WSL scenarios and security analysis.

Heavy suites do not need to run on every commit when that provides poor feedback, but they must run regularly and before releases.

## Local validation
From the repository root on the current feature branch:

```text
dotnet restore
dotnet build src/WorkspaceControl.Desktop/WorkspaceControl.Desktop.csproj --configuration Debug --runtime win-x64
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj --no-restore --verify-no-changes
```

## Quality rule
Fix warnings, flaky tests and errors at the source. Do not disable tests or analyzers merely to obtain green CI.
