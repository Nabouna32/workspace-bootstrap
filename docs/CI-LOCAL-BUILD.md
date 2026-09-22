# CI and local builds

## Source of truth
GitHub is the source of truth. The exact commit being merged must be validated by GitHub Actions.

## Runner policy
All workflows use **GitHub-hosted Windows runners**. **Self-hosted runners are not part of Workspace Control.**

## PR validation
Fast validation covers restore, build, unit tests, architecture/static analysis, formatting and contract/schema checks.

## Main / scheduled validation
Heavier validation may run on main, scheduled or manual workflows: full Release builds, published artifacts, CLI smoke tests, Windows integration, provisioning simulations, cache/offline scenarios, install/update/remove integration, UI/accessibility tests, WSL scenarios and security analysis.

Heavy suites do not need to run on every commit when that provides poor feedback, but they must run regularly and before releases.

## Local validation
```text
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Once the WinUI application exists, this document must include its exact branch-specific build/run command.

## Quality rule
Fix warnings, flaky tests and errors at the source. Do not disable tests or analyzers merely to obtain green CI.