# CI and local builds

## Source of truth

GitHub is the source of truth. Development work happens on feature branches and is validated before merge.

## Runner policy

All repository workflows run on the project's self-hosted Windows x64 runner:

`[self-hosted, windows, x64]`

There are no GitHub-hosted runner dependencies.

## Local build

From the repository root on the current feature branch:

```text
dotnet restore tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj
dotnet restore desktop/WorkspaceBootstrap.Desktop.csproj
dotnet restore src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj

dotnet build src/WorkspaceBootstrap.Engine/WorkspaceBootstrap.Engine.csproj --configuration Release --no-restore
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Release --no-restore
dotnet build src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release --no-restore

dotnet test tests/WorkspaceBootstrap.Tests/WorkspaceBootstrap.Tests.csproj --configuration Release --no-restore
dotnet publish src/WorkspaceBootstrap.Cli/WorkspaceBootstrap.Cli.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore
```

## Validation policy

A green result must come from the exact commit being merged. Do not rely on an older workflow run.

Warnings and errors must be fixed at their source. Do not disable analyzers or suppress failures just to obtain a green run.

## Release validation

Release packaging should additionally verify:

- self-contained Windows x64 startup;
- component/profile catalog loading;
- cache-only behavior;
- installer signature validation;
- recovery after interrupted provisioning;
- clean Windows smoke installation in an isolated environment.
