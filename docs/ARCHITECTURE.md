# Architecture

Workspace Bootstrap is a Windows 11 workstation provisioning and recovery application.

## Runtime

- C# / .NET 10 is the application technology.
- The shared Engine owns domain logic, Windows integration, provisioning, installer resolution, cache, verification, diagnostics, maintenance, optimization and operation recovery.
- The desktop application is a native Windows presentation layer over the Engine.
- The CLI is a self-contained Windows x64 entry point over the same Engine.
- There is no script-based product backend.

## Boundaries

```text
Desktop / CLI
     │
     ▼
WorkspaceBootstrap.Engine
     ├── Configuration
     ├── Catalog / Profiles
     ├── Inventory / Diagnostics
     ├── Installer resolution
     ├── Cache / Verification
     ├── Provisioning / Recovery
     └── Windows integration
```

External vendor installers, WinGet, Windows APIs, Registry and filesystem access are infrastructure boundaries. Their use is orchestrated by C#.

## CI

GitHub Actions is the source of truth for validation. All jobs use standard GitHub-hosted runners.
