# Workspace Bootstrap Desktop

Native Windows desktop application for Workspace Bootstrap.

## Architecture

- UI: WPF on .NET 10.
- Application logic: shared `WorkspaceBootstrap.Engine`.
- Provisioning: Engine-owned; the desktop does not execute scripts.
- CLI and desktop share the same domain and provisioning implementation.
- Persistent state: `C:\Dev\WorkspaceBootstrap`.
- Installer cache: `C:\DevCache`.

## Development

From the repository root:

```text
dotnet restore desktop/WorkspaceBootstrap.Desktop.csproj
dotnet build desktop/WorkspaceBootstrap.Desktop.csproj --configuration Debug
dotnet run --project desktop/WorkspaceBootstrap.Desktop.csproj --configuration Debug
```

## UX direction

The desktop is a real Windows 11 management application:

- dashboard-first navigation;
- consistent cards and state indicators;
- profile/component selection;
- plan preview before mutation;
- visible progress and recovery;
- operation history;
- diagnostics and inventory;
- guarded maintenance/optimization;
- keyboard-friendly, accessible controls;
- dark/light-ready design tokens.

The presentation layer must not duplicate Engine behavior.
