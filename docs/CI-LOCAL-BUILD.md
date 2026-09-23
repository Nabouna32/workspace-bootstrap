# CI and local builds

## Source of truth
GitHub is the source of truth. The exact commit being merged must be validated by GitHub Actions.

## Runner policy
All workflows use **GitHub-hosted Windows runners**. **Self-hosted runners are not part of Workspace Control and must not be reintroduced.**

## PR validation
Fast validation covers restore, build, unit tests, architecture/static analysis, formatting and contract/schema checks.

The repository validates the native WinUI desktop and the shared .NET application/infrastructure stack. WPF is retired.

## Main / scheduled validation
Heavier validation runs on main, scheduled or manual workflows:
- full Debug/Release builds;
- published artifacts;
- CLI smoke tests;
- Windows package launch validation;
- Windows integration and provisioning simulations;
- artifact reuse scenarios;
- install/update/remove integration;
- UI/accessibility contract checks;
- WSL scenarios;
- security analysis.

Heavy suites do not need to run on every commit when that provides poor feedback, but they must run regularly and before releases.

### Published Windows validation
`.github/workflows/windows-published-validation.yml` is the focused Windows package validation path. It runs weekly and on demand. It:
1. builds the WinUI desktop and shared application stack on `windows-latest`;
2. runs the release test suite;
3. publishes a self-contained `win-x64` package;
4. verifies required package files and localized resource payloads;
5. runs the published CLI capability smoke test;
6. launches the published WinUI executable and verifies that it remains alive for the smoke-test window;
7. uploads the validated package and test results as workflow artifacts.

This verifies packaging and process-level startup. It does **not** replace interactive Windows 11 validation of navigation, visual layout, keyboard/focus behavior, screen-reader output, localization switching or DPI/scaling.

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
