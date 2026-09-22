# Workspace Bootstrap Desktop

Native WPF desktop client for Workspace Bootstrap.

## Architecture

The desktop application is presentation and interaction. Provisioning, installer resolution, cache handling, Windows diagnostics and operation state belong to the shared C# engine.

Build:

```text
dotnet build .\WorkspaceBootstrap.Desktop.csproj --configuration Release
```

Run:

```text
dotnet run --project .\WorkspaceBootstrap.Desktop.csproj
```

The desktop client communicates with the self-contained Workspace Bootstrap CLI when running from a published installation, and can use `dotnet run` against the CLI project during development.

There is no PowerShell execution backend.
